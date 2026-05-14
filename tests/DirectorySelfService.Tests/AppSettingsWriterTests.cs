using DirectorySelfService.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectorySelfService.Tests;

[TestClass]
public sealed class AppSettingsWriterTests
{
    [TestMethod]
    public void SplitLinesAcceptsLinesCommasAndSemicolons()
    {
        var values = AppSettingsWriter.SplitLines("Self Service Users\nHelpdesk Users,Domain Admins;Enterprise Admins");

        CollectionAssert.AreEqual(
            new[] { "Self Service Users", "Helpdesk Users", "Domain Admins", "Enterprise Admins" },
            values);
    }

    [TestMethod]
    public void SplitLinesRemovesDuplicatesIgnoringCase()
    {
        var values = AppSettingsWriter.SplitLines("Domain Admins\ndomain admins\nEnterprise Admins");

        CollectionAssert.AreEqual(new[] { "Domain Admins", "Enterprise Admins" }, values);
    }
}
