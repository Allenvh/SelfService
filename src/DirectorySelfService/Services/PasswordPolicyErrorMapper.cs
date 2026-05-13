using System.DirectoryServices.Protocols;
using System.Text.RegularExpressions;
using DirectorySelfService.Models;

namespace DirectorySelfService.Services;

public sealed class PasswordPolicyErrorMapper
{
    private static readonly Regex DataCodeRegex = new(@"data\s(?<code>[0-9a-fA-F]{3,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PasswordChangeResult MapDirectoryException(DirectoryOperationException ex) =>
        MapDiagnosticMessage(ex.Response?.ErrorMessage ?? ex.Message, ex.Response?.ResultCode);

    public PasswordChangeResult MapLdapException(LdapException ex) =>
        MapDiagnosticMessage(ex.ServerErrorMessage ?? ex.Message, resultCode: null, isInvalidCredentials: ex.ErrorCode == 49);

    public PasswordChangeResult MapDiagnosticMessage(string diagnosticMessage, ResultCode? resultCode = null, bool isInvalidCredentials = false)
    {
        var message = diagnosticMessage ?? string.Empty;
        var lower = message.ToLowerInvariant();
        var dataCode = ExtractDataCode(lower);

        return dataCode switch
        {
            "525" => PasswordChangeResult.Fail(PasswordChangeResultCategory.UserNotFound, "We could not find that user account."),
            "52e" => PasswordChangeResult.Fail(PasswordChangeResultCategory.InvalidCurrentPassword, "The current password is not correct."),
            "530" or "531" => PasswordChangeResult.Fail(PasswordChangeResultCategory.NotAllowedByGroup, "Password changes are not allowed from this location or workstation."),
            "532" or "773" => PasswordChangeResult.Fail(PasswordChangeResultCategory.PasswordExpired, "Your current password is expired. Contact the service desk if this page cannot complete the change."),
            "533" => PasswordChangeResult.Fail(PasswordChangeResultCategory.DisabledAccount, "This account is disabled. Contact the service desk."),
            "775" => PasswordChangeResult.Fail(PasswordChangeResultCategory.LockedAccount, "This account is locked. Wait and try again, or contact the service desk."),
            _ when IsPasswordRestriction(resultCode, lower) => MapPasswordRestriction(lower),
            _ when isInvalidCredentials => PasswordChangeResult.Fail(PasswordChangeResultCategory.InvalidCurrentPassword, "The current password is not correct."),
            _ => PasswordChangeResult.Fail(PasswordChangeResultCategory.UnknownFailure, "The password could not be changed. Verify the information and try again.")
        };
    }

    private static string ExtractDataCode(string message)
    {
        var match = DataCodeRegex.Match(message);
        return match.Success ? match.Groups["code"].Value.ToLowerInvariant() : string.Empty;
    }

    private static bool IsPasswordRestriction(ResultCode? resultCode, string message) =>
        resultCode is ResultCode.ConstraintViolation or ResultCode.UnwillingToPerform ||
        message.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("unwilling", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static PasswordChangeResult MapPasswordRestriction(string message)
    {
        if (message.Contains("history", StringComparison.OrdinalIgnoreCase))
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.PasswordHistoryViolation, "Choose a password you have not used recently.");
        }

        if (message.Contains("minimum password age", StringComparison.OrdinalIgnoreCase) || message.Contains("minpwdage", StringComparison.OrdinalIgnoreCase))
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.MinimumAgeViolation, "Your password was changed too recently. Try again later or contact the service desk.");
        }

        return PasswordChangeResult.Fail(PasswordChangeResultCategory.ComplexityFailure, "The new password does not meet the password policy requirements.");
    }
}
