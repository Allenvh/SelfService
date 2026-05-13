using DirectorySelfService.Models;
using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Pages;

public sealed class IndexModel(
    IActiveDirectoryPasswordService passwordService,
    IRateLimitService rateLimitService,
    IAuditLogger auditLogger,
    IOptions<CaptchaOptions> captchaOptions) : PageModel
{
    [BindProperty]
    public PasswordChangeRequest Input { get; set; } = new();

    public string? StatusMessage { get; private set; }
    public bool IsSuccess { get; private set; }
    public bool CaptchaEnabled => captchaOptions.Value.Enabled;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var limited = rateLimitService.Check(sourceIp, Input.Username);
        if (limited is not null)
        {
            StatusMessage = limited.UserMessage;
            IsSuccess = false;
            auditLogger.PasswordChangeAttempt(Input.Username, sourceIp, limited);
            return Page();
        }

        var result = await passwordService.ChangePasswordAsync(Input, cancellationToken);
        auditLogger.PasswordChangeAttempt(Input.Username, sourceIp, result);

        if (result.Succeeded)
        {
            rateLimitService.RecordSuccess(sourceIp, Input.Username);
            ModelState.Clear();
            Input = new PasswordChangeRequest();
        }
        else
        {
            rateLimitService.RecordFailure(sourceIp, Input.Username);
        }

        StatusMessage = result.UserMessage;
        IsSuccess = result.Succeeded;
        return Page();
    }
}
