namespace DirectorySelfService.Options;

public sealed class CaptchaOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string SiteKey { get; set; } = string.Empty;
}
