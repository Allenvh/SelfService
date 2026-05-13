using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using DirectorySelfService.Models;
using DirectorySelfService.Options;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class ActiveDirectoryPasswordService(
    IOptions<DirectoryOptions> options,
    UsernameNormalizer normalizer,
    PasswordPolicyErrorMapper errorMapper,
    ILogger<ActiveDirectoryPasswordService> logger) : IActiveDirectoryPasswordService
{
    private readonly DirectoryOptions _options = options.Value;

    public Task<PasswordChangeResult> ChangePasswordAsync(PasswordChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => ChangePassword(request, cancellationToken), cancellationToken);
    }

    private PasswordChangeResult ChangePassword(PasswordChangeRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.InvalidRequest, "The new passwords do not match.");
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.InvalidRequest, "Choose a new password that is different from your current password.");
        }

        try
        {
            using var connection = CreateConnection();
            connection.Bind(new NetworkCredential(normalizer.ToBindIdentity(request.Username), request.CurrentPassword));
            cancellationToken.ThrowIfCancellationRequested();

            var entry = FindUser(connection, request.Username);
            if (entry is null)
            {
                return PasswordChangeResult.Fail(PasswordChangeResultCategory.UserNotFound, "We could not find that user account.");
            }

            var groupResult = ValidateGroupMembership(entry);
            if (groupResult is not null)
            {
                return groupResult;
            }

            var distinguishedName = GetString(entry, "distinguishedName");
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                logger.LogWarning("Directory entry found without a distinguishedName for a password change request.");
                return PasswordChangeResult.Fail(PasswordChangeResultCategory.UnknownFailure, "The password could not be changed. Contact the service desk.");
            }

            ChangeUserPassword(connection, distinguishedName, request.CurrentPassword, request.NewPassword);
            return PasswordChangeResult.Success("Your password has been changed.");
        }
        catch (DirectoryOperationException ex)
        {
            logger.LogWarning(ex, "Directory rejected a password change request with result code {ResultCode}.", ex.Response?.ResultCode);
            return errorMapper.MapDirectoryException(ex);
        }
        catch (LdapException ex)
        {
            logger.LogWarning(ex, "LDAP operation failed with error code {ErrorCode}.", ex.ErrorCode);
            return errorMapper.MapLdapException(ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected directory password change failure.");
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.DirectoryUnavailable, "The directory service is unavailable. Try again later.");
        }
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_options.LdapServer, _options.LdapPort, true, false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Negotiate,
            Timeout = TimeSpan.FromSeconds(_options.LdapTimeoutSeconds)
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = _options.UseSsl;
        if (!_options.UseSsl)
        {
            logger.LogWarning("LDAP SSL is disabled. Active Directory password changes normally require LDAPS or another encrypted channel.");
        }
        return connection;
    }

    private SearchResultEntry? FindUser(LdapConnection connection, string username)
    {
        var searchValue = normalizer.ToSearchValue(username);
        var escaped = EscapeLdapFilterValue(searchValue);
        var filter = searchValue.Contains('@')
            ? $"(&(objectClass=user)(userPrincipalName={escaped}))"
            : $"(&(objectClass=user)(sAMAccountName={escaped}))";
        var request = new SearchRequest(
            _options.SearchBaseDn,
            filter,
            SearchScope.Subtree,
            "distinguishedName",
            "memberOf",
            "userAccountControl",
            "lockoutTime");
        var response = (SearchResponse)connection.SendRequest(request);
        return response.Entries.Count == 0 ? null : response.Entries[0];
    }

    private PasswordChangeResult? ValidateGroupMembership(SearchResultEntry entry)
    {
        var memberOf = GetValues(entry, "memberOf").ToArray();
        if (_options.RestrictedGroups.Any() && memberOf.Any(group => MatchesConfiguredGroup(group, _options.RestrictedGroups)))
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.RestrictedGroup, "This account cannot use self-service password changes. Contact the service desk.");
        }

        if (_options.AllowedGroups.Any() && !memberOf.Any(group => MatchesConfiguredGroup(group, _options.AllowedGroups)))
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.NotAllowedByGroup, "This account is not enabled for self-service password changes.");
        }

        var userAccountControl = GetInt64(entry, "userAccountControl");
        if ((userAccountControl & 0x2) == 0x2)
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.DisabledAccount, "This account is disabled. Contact the service desk.");
        }

        var lockoutTime = GetInt64(entry, "lockoutTime");
        if (lockoutTime > 0)
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.LockedAccount, "This account is locked. Wait and try again, or contact the service desk.");
        }

        return null;
    }

    private static void ChangeUserPassword(LdapConnection connection, string distinguishedName, string currentPassword, string newPassword)
    {
        var modify = new ModifyRequest(distinguishedName,
            new DirectoryAttributeModification
            {
                Name = "unicodePwd",
                Operation = DirectoryAttributeOperation.Delete
            },
            new DirectoryAttributeModification
            {
                Name = "unicodePwd",
                Operation = DirectoryAttributeOperation.Add
            });

        modify.Modifications[0].Add(EncodePassword(currentPassword));
        modify.Modifications[1].Add(EncodePassword(newPassword));
        connection.SendRequest(modify);
    }

    private static byte[] EncodePassword(string password) => Encoding.Unicode.GetBytes($"\"{password}\"");

    private static IEnumerable<string> GetValues(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            yield break;
        }

        foreach (var value in entry.Attributes[attributeName])
        {
            if (value is byte[] bytes)
            {
                yield return Encoding.UTF8.GetString(bytes);
            }
            else if (value is not null)
            {
                yield return value.ToString() ?? string.Empty;
            }
        }
    }

    private static string GetString(SearchResultEntry entry, string attributeName) => GetValues(entry, attributeName).FirstOrDefault() ?? string.Empty;

    private static long GetInt64(SearchResultEntry entry, string attributeName) => long.TryParse(GetString(entry, attributeName), out var value) ? value : 0;

    private static bool MatchesConfiguredGroup(string groupDn, IEnumerable<string> configuredGroups) => configuredGroups.Any(configured =>
        groupDn.Equals(configured, StringComparison.OrdinalIgnoreCase) ||
        groupDn.StartsWith($"CN={configured},", StringComparison.OrdinalIgnoreCase));

    private static string EscapeLdapFilterValue(string value) => value
        .Replace("\\", "\\5c", StringComparison.Ordinal)
        .Replace("*", "\\2a", StringComparison.Ordinal)
        .Replace("(", "\\28", StringComparison.Ordinal)
        .Replace(")", "\\29", StringComparison.Ordinal)
        .Replace("\0", "\\00", StringComparison.Ordinal);
}
