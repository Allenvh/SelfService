using DirectorySelfService.Options;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class UsernameNormalizer(IOptions<DirectoryOptions> options)
{
    private readonly DirectoryOptions _options = options.Value;

    public string NormalizeForAudit(string username) => username.Trim().ToLowerInvariant();

    public string ToBindIdentity(string username)
    {
        var trimmed = username.Trim();
        if (trimmed.Contains('@') || trimmed.Contains('\\') || string.IsNullOrWhiteSpace(_options.DefaultDomain))
        {
            return trimmed;
        }

        return $@"{_options.DefaultDomain}\{trimmed}";
    }

    public string ToSearchValue(string username)
    {
        var trimmed = username.Trim();
        var slashIndex = trimmed.LastIndexOf('\\');
        return slashIndex >= 0 ? trimmed[(slashIndex + 1)..] : trimmed;
    }
}
