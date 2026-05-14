using DirectorySelfService.Models;

namespace DirectorySelfService.Services;

public interface IAppSettingsWriter
{
    Task SaveDirectorySettingsAsync(AdminDirectorySettings settings, CancellationToken cancellationToken);
}
