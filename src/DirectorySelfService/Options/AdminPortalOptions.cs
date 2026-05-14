namespace DirectorySelfService.Options;

public sealed class AdminPortalOptions
{
    public bool Enabled { get; set; }
    public string SharedSecret { get; set; } = string.Empty;
    public string WritableSettingsPath { get; set; } = "appsettings.json";
}
