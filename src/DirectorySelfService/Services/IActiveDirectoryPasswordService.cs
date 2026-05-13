using DirectorySelfService.Models;

namespace DirectorySelfService.Services;

public interface IActiveDirectoryPasswordService
{
    Task<PasswordChangeResult> ChangePasswordAsync(PasswordChangeRequest request, CancellationToken cancellationToken);
}
