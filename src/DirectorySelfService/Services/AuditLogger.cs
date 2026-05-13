using System.Security.Cryptography;
using System.Text;
using DirectorySelfService.Models;
using DirectorySelfService.Options;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class AuditLogger(ILogger<AuditLogger> logger, IOptions<AuditOptions> options, UsernameNormalizer normalizer) : IAuditLogger
{
    private readonly AuditOptions _options = options.Value;

    public void PasswordChangeAttempt(string username, string sourceIp, PasswordChangeResult result)
    {
        var normalized = normalizer.NormalizeForAudit(username);
        var auditUser = _options.HashUsernames ? HashUsername(normalized) : normalized;
        logger.LogInformation("PasswordChangeAttempt timestamp={Timestamp:o} user={AuditUser} sourceIp={SourceIp} category={Category} success={Success}",
            DateTimeOffset.UtcNow,
            auditUser,
            sourceIp,
            result.Category,
            result.Succeeded);
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
}
