namespace DirectorySelfService.Options;

public sealed class DirectoryOptions
{
    public string DefaultDomain { get; set; } = string.Empty;
    public string LdapServer { get; set; } = string.Empty;
    public int LdapPort { get; set; } = 636;
    public bool UseSsl { get; set; } = true;
    public string SearchBaseDn { get; set; } = string.Empty;
    public string[] AllowedGroups { get; set; } = [];
    public string[] RestrictedGroups { get; set; } = [];
    public int LdapTimeoutSeconds { get; set; } = 15;
}
