using DirectorySelfService.Models;
using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectorySelfService.Tests;

[TestClass]
public sealed class RateLimitServiceTests
{
    [TestMethod]
    public void LimitsAfterConfiguredFailuresByUsername()
    {
        var service = new RateLimitService(Options.Create(new RateLimitOptions
        {
            PermitLimit = 100,
            UsernamePermitLimit = 2,
            WindowMinutes = 15
        }), TimeProvider.System);

        Assert.IsNull(service.Check("10.0.0.1", "user@example.com"));
        service.RecordFailure("10.0.0.1", "user@example.com");
        service.RecordFailure("10.0.0.2", "USER@example.com");

        var result = service.Check("10.0.0.3", "user@example.com");
        Assert.IsNotNull(result);
        Assert.AreEqual(PasswordChangeResultCategory.RateLimited, result.Category);
    }

    [TestMethod]
    public void SuccessClearsFailureWindow()
    {
        var service = new RateLimitService(Options.Create(new RateLimitOptions
        {
            PermitLimit = 1,
            UsernamePermitLimit = 1,
            WindowMinutes = 15
        }), TimeProvider.System);

        service.RecordFailure("10.0.0.1", "user");
        Assert.IsNotNull(service.Check("10.0.0.1", "user"));

        service.RecordSuccess("10.0.0.1", "user");
        Assert.IsNull(service.Check("10.0.0.1", "user"));
    }
}
