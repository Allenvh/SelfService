using DirectorySelfService.Models;

namespace DirectorySelfService.Services;

public interface IAuditLogger
{
    void PasswordChangeAttempt(string username, string sourceIp, PasswordChangeResult result);
}
