using System.Security.Cryptography;
using System.Text;
using DirectorySelfService.Models;
using DirectorySelfService.Options;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class AuditLogger(ILogger<AuditLogger> logger, IOptions<AuditOptions> options, UsernameNormalizer normalizer) : IAuditLogger
{
    private readonly object _textLogLock = new();
    private readonly AuditOptions _options = options.Value;

    public void PasswordChangeAttempt(string username, string sourceIp, PasswordChangeResult result)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var normalized = normalizer.NormalizeForAudit(username);
        var auditUser = _options.HashUsernames ? HashUsername(normalized) : normalized;
        logger.LogInformation("PasswordChangeAttempt timestamp={Timestamp:o} user={AuditUser} sourceIp={SourceIp} category={Category} success={Success}",
            timestamp,
            auditUser,
            sourceIp,
            result.Category,
            result.Succeeded);

        try
        {
            WriteTextLog(timestamp, auditUser, sourceIp, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write password change audit text log to {TextLogPath}", _options.TextLogPath);
        }
    }

    private void WriteTextLog(DateTimeOffset timestamp, string auditUser, string sourceIp, PasswordChangeResult result)
    {
        if (string.IsNullOrWhiteSpace(_options.TextLogPath))
        {
            return;
        }

        var logDirectory = Path.GetDirectoryName(_options.TextLogPath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var logLine = $"PasswordChangeAttempt timestamp={timestamp:o} user={SanitizeAuditValue(auditUser)} sourceIp={SanitizeAuditValue(sourceIp)} category={result.Category} success={result.Succeeded}{Environment.NewLine}";
        lock (_textLogLock)
        {
            File.AppendAllText(_options.TextLogPath, logLine, Encoding.UTF8);
        }
    }

    private string HashUsername(string normalizedUsername)
    {
        var saltBytes = Encoding.UTF8.GetBytes(_options.UsernameHashSalt);
        var usernameBytes = Encoding.UTF8.GetBytes(normalizedUsername);
        var data = new byte[saltBytes.Length + usernameBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, data, 0, saltBytes.Length);
        Buffer.BlockCopy(usernameBytes, 0, data, saltBytes.Length, usernameBytes.Length);
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static string SanitizeAuditValue(string value) => value
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('\t', ' ');
}
