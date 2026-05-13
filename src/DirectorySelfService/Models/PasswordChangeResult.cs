namespace DirectorySelfService.Models;

public sealed record PasswordChangeResult(
    bool Succeeded,
    PasswordChangeResultCategory Category,
    string UserMessage)
{
    public static PasswordChangeResult Success(string message) => new(true, PasswordChangeResultCategory.Success, message);
    public static PasswordChangeResult Fail(PasswordChangeResultCategory category, string message) => new(false, category, message);
}

public enum PasswordChangeResultCategory
{
    Success,
    InvalidRequest,
    InvalidCurrentPassword,
    UserNotFound,
    DisabledAccount,
    LockedAccount,
    PasswordExpired,
    ComplexityFailure,
    PasswordHistoryViolation,
    MinimumAgeViolation,
    NotAllowedByGroup,
    RestrictedGroup,
    RateLimited,
    DirectoryUnavailable,
    UnknownFailure
}
