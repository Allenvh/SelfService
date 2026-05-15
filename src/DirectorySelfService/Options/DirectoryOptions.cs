namespace DirectorySelfService.Options;

public sealed class DirectoryOptions
{
    public string DefaultDomain { get; set; } = string.Empty;
    public string LdapServer { get; set; } = string.Empty;
    public int LdapPort { get; set; } = 389;
    public bool UseSsl { get; set; }
    public bool UseSigning { get; set; } = true;
    public bool UseSealing { get; set; } = true;
    public string SearchBaseDn { get; set; } = string.Empty;
    public string FirstLoginLookupUser { get; set; } = string.Empty;
    public string FirstLoginLookupPassword { get; set; } = string.Empty;
    public string[] AllowedGroups { get; set; } = [];
    public string[] RestrictedGroups { get; set; } = [];
    public int LdapTimeoutSeconds { get; set; } = 15;
}
