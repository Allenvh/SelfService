using System.DirectoryServices.Protocols;
using DirectorySelfService.Models;
using DirectorySelfService.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectorySelfService.Tests;

[TestClass]
public sealed class PasswordPolicyErrorMapperTests
{
    private readonly PasswordPolicyErrorMapper _mapper = new();

    [TestMethod]
    public void MapsInvalidCredentialsDataCodeToInvalidCurrentPassword()
    {
        var result = _mapper.MapDiagnosticMessage("AcceptSecurityContext error, data 52e, v2580", ResultCode.InvalidCredentials);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(PasswordChangeResultCategory.InvalidCurrentPassword, result.Category);
    }

    [TestMethod]
    public void MapsLockedAccountDataCodeToLockedAccount()
    {
        var result = _mapper.MapDiagnosticMessage("LDAP error data 775", ResultCode.InvalidCredentials);
        Assert.AreEqual(PasswordChangeResultCategory.LockedAccount, result.Category);
    }

    [TestMethod]
    public void MapsConstraintViolationToComplexityFailure()
    {
        var result = _mapper.MapDiagnosticMessage("0000052D: Constraint violation", ResultCode.ConstraintViolation);
        Assert.AreEqual(PasswordChangeResultCategory.ComplexityFailure, result.Category);
    }

    [TestMethod]
    public void MapsHistoryMessageToHistoryViolation()
    {
        var result = _mapper.MapDiagnosticMessage("password history requirement was not met", ResultCode.ConstraintViolation);
        Assert.AreEqual(PasswordChangeResultCategory.PasswordHistoryViolation, result.Category);
    }
}
