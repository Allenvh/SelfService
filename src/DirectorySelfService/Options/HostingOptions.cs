namespace DirectorySelfService.Options;

public sealed class HostingOptions
{
    public int? HttpsPort { get; set; } = 443;
    public string DataProtectionKeysPath { get; set; } = @"C:\ProgramData\DirectorySelfService\DataProtectionKeys";
    public string DataProtectionApplicationName { get; set; } = "DirectorySelfService";
}
