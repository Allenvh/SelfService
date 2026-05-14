using DirectorySelfService.Models;
using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectorySelfService.Tests;

[TestClass]
public sealed class AuditLoggerTests
{
    [TestMethod]
    public void PasswordChangeAttemptWritesConfiguredTextLog()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"DirectorySelfServiceTests-{Guid.NewGuid():N}");
        var logPath = Path.Combine(tempDirectory, "audit.log");
        try
        {
            var logger = CreateAuditLogger(logPath, hashUsernames: false);

            logger.PasswordChangeAttempt(" Alice ", "192.0.2.10", PasswordChangeResult.Success("Password changed."));

            var logContents = File.ReadAllText(logPath);
            StringAssert.Contains(logContents, "PasswordChangeAttempt");
            StringAssert.Contains(logContents, "user=alice");
            StringAssert.Contains(logContents, "sourceIp=192.0.2.10");
            StringAssert.Contains(logContents, "category=Success");
            StringAssert.Contains(logContents, "success=True");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void PasswordChangeAttemptOmitsTextLogWhenNoPathIsConfigured()
    {
        var logger = CreateAuditLogger(textLogPath: string.Empty, hashUsernames: false);

        logger.PasswordChangeAttempt("Alice", "192.0.2.10", PasswordChangeResult.Fail(PasswordChangeResultCategory.InvalidCurrentPassword, "Invalid."));
    }

    private static AuditLogger CreateAuditLogger(string textLogPath, bool hashUsernames)
    {
        var auditOptions = Options.Create(new AuditOptions
        {
            HashUsernames = hashUsernames,
            TextLogPath = textLogPath
        });
        var directoryOptions = Options.Create(new DirectoryOptions { DefaultDomain = "CONTOSO" });
        var normalizer = new UsernameNormalizer(directoryOptions);
        return new AuditLogger(NullLogger<AuditLogger>.Instance, auditOptions, normalizer);
    }
}
