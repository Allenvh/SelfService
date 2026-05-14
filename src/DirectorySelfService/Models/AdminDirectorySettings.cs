using System.ComponentModel.DataAnnotations;

namespace DirectorySelfService.Models;

public sealed class AdminDirectorySettings
{
    [Display(Name = "Default domain / NetBIOS name")]
    public string DefaultDomain { get; set; } = string.Empty;

    [Required]
    [Display(Name = "LDAP server")]
    public string LdapServer { get; set; } = string.Empty;

    [Range(1, 65535)]
    [Display(Name = "LDAP port")]
    public int LdapPort { get; set; } = 389;

    [Display(Name = "Use SSL / LDAPS")]
    public bool UseSsl { get; set; }

    [Display(Name = "Use LDAP signing")]
    public bool UseSigning { get; set; } = true;

    [Display(Name = "Use LDAP sealing")]
    public bool UseSealing { get; set; } = true;

    [Required]
    [Display(Name = "Search base DN")]
    public string SearchBaseDn { get; set; } = string.Empty;

    [Display(Name = "LDAP timeout seconds")]
    [Range(1, 120)]
    public int LdapTimeoutSeconds { get; set; } = 15;

    [Display(Name = "Allowed groups")]
    public string AllowedGroupsText { get; set; } = string.Empty;

    [Display(Name = "Restricted groups")]
    public string RestrictedGroupsText { get; set; } = string.Empty;

    [Display(Name = "Enable verbose troubleshooting logs")]
    public bool VerboseDirectoryLogging { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Admin shared secret")]
    public string SharedSecret { get; set; } = string.Empty;
}
