namespace DirectorySelfService.Options;

public sealed class AuditOptions
{
    public bool HashUsernames { get; set; } = true;
    public string UsernameHashSalt { get; set; } = "change-this-salt";
    public bool EnableWindowsEventLog { get; set; } = false;
    public string EventLogSource { get; set; } = "DirectorySelfService";
}
