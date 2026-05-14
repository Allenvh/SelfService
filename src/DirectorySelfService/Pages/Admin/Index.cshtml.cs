using DirectorySelfService.Models;
using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Pages.Admin;

public sealed class IndexModel(
    IOptionsSnapshot<DirectoryOptions> directoryOptions,
    IOptionsSnapshot<AdminPortalOptions> adminOptions,
    IConfiguration configuration,
    IAppSettingsWriter settingsWriter,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public AdminDirectorySettings Input { get; set; } = new();

    public string? StatusMessage { get; private set; }
    public bool IsSuccess { get; private set; }
    public bool PortalEnabled => adminOptions.Value.Enabled;
    public bool HasSharedSecret => !string.IsNullOrWhiteSpace(adminOptions.Value.SharedSecret);

    public void OnGet()
    {
        PopulateFromCurrentSettings(preserveSecret: false);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!PortalEnabled)
        {
            StatusMessage = "The admin portal is disabled. Set AdminPortal:Enabled to true to use this page.";
            PopulateFromCurrentSettings(preserveSecret: true);
            return Page();
        }

        if (!HasSharedSecret || !CryptographicEquals(Input.SharedSecret, adminOptions.Value.SharedSecret))
        {
            logger.LogWarning("Rejected admin directory settings update from {SourceIp}: invalid shared secret.", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            ModelState.AddModelError(nameof(Input.SharedSecret), "The admin shared secret is invalid.");
        }

        if (!ModelState.IsValid)
        {
            Input.SharedSecret = string.Empty;
            return Page();
        }

        await settingsWriter.SaveDirectorySettingsAsync(Input, cancellationToken);
        logger.LogInformation("Admin directory settings updated from {SourceIp}. LdapServer={LdapServer}; SearchBaseDn={SearchBaseDn}; UseSsl={UseSsl}; UseSigning={UseSigning}; UseSealing={UseSealing}; AllowedGroupCount={AllowedGroupCount}; RestrictedGroupCount={RestrictedGroupCount}; VerboseDirectoryLogging={VerboseDirectoryLogging}",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Input.LdapServer,
            Input.SearchBaseDn,
            Input.UseSsl,
            Input.UseSigning,
            Input.UseSealing,
            AppSettingsWriter.SplitLines(Input.AllowedGroupsText).Length,
            AppSettingsWriter.SplitLines(Input.RestrictedGroupsText).Length,
            Input.VerboseDirectoryLogging);

        StatusMessage = "Directory settings were saved. ASP.NET Core reloads JSON changes automatically; recycle the IIS app pool if environment-variable overrides are still taking precedence.";
        IsSuccess = true;
        PopulateFromCurrentSettings(preserveSecret: false);
        return Page();
    }

    private void PopulateFromCurrentSettings(bool preserveSecret)
    {
        var directory = directoryOptions.Value;
        var sharedSecret = preserveSecret ? Input.SharedSecret : string.Empty;
        Input = new AdminDirectorySettings
        {
            DefaultDomain = directory.DefaultDomain,
            LdapServer = directory.LdapServer,
            LdapPort = directory.LdapPort,
            UseSsl = directory.UseSsl,
            UseSigning = directory.UseSigning,
            UseSealing = directory.UseSealing,
            SearchBaseDn = directory.SearchBaseDn,
            LdapTimeoutSeconds = directory.LdapTimeoutSeconds,
            AllowedGroupsText = string.Join(Environment.NewLine, directory.AllowedGroups),
            RestrictedGroupsText = string.Join(Environment.NewLine, directory.RestrictedGroups),
            VerboseDirectoryLogging = IsVerboseLoggingEnabled(),
            SharedSecret = sharedSecret
        };
    }

    private bool IsVerboseLoggingEnabled()
    {
        var serviceLevel = configuration["Logging:LogLevel:DirectorySelfService.Services.ActiveDirectoryPasswordService"];
        var protocolLevel = configuration["Logging:LogLevel:System.DirectoryServices.Protocols"];
        return IsVerbose(serviceLevel) || IsVerbose(protocolLevel);
    }

    private static bool IsVerbose(string? level) =>
        string.Equals(level, "Trace", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(level, "Debug", StringComparison.OrdinalIgnoreCase);

    private static bool CryptographicEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
