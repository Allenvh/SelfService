using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectorySelfService.Tests;

[TestClass]
public sealed class UsernameNormalizerTests
{
    [TestMethod]
    public void AddsDefaultDomainForSamAccountName()
    {
        var normalizer = new UsernameNormalizer(Options.Create(new DirectoryOptions { DefaultDomain = "CONTOSO" }));
        Assert.AreEqual(@"CONTOSO\alice", normalizer.ToBindIdentity("alice"));
    }

    [TestMethod]
    public void LeavesUpnUnchangedForBindIdentity()
    {
        var normalizer = new UsernameNormalizer(Options.Create(new DirectoryOptions { DefaultDomain = "CONTOSO" }));
        Assert.AreEqual("alice@example.com", normalizer.ToBindIdentity("alice@example.com"));
    }
}
