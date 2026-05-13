using DirectorySelfService.Models;

namespace DirectorySelfService.Services;

public interface IRateLimitService
{
    PasswordChangeResult? Check(string sourceIp, string username);
    void RecordFailure(string sourceIp, string username);
    void RecordSuccess(string sourceIp, string username);
}
