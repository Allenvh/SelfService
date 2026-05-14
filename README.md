# Directory Self-Service Password Portal

This repository contains an original ASP.NET Core application for an internal, company-owned Active Directory password change portal. It is designed for domain-joined Windows Server and IIS hosting. The implementation uses documented Microsoft/.NET APIs and does not import, reference, fork, copy, or depend on any third-party self-service password reset project.

## Features

- Razor Pages password change form for username/UPN, current password, new password, and confirmation.
- Active Directory password change over LDAP using `System.DirectoryServices.Protocols`, with LDAPS disabled by default and LDAP signing/sealing enabled for protected communication.
- User-context password change flow: the application binds with the submitted identity and current password, then issues an LDAP `unicodePwd` delete/add modify request.
- Configurable domain, LDAP server, LDAP port, SSL, search base DN, allowed groups, and restricted groups.
- Optional admin settings portal for updating domain configuration, allowed groups, restricted groups, LDAP communication settings, and temporary verbose troubleshooting logs.
- Friendly error mapping for invalid credentials, missing users, disabled or locked accounts, expired password states, complexity failures, password history, and minimum-age failures.
- HTTPS redirection, HSTS, CSRF validation, secure HTTP headers, per-IP and per-username rate limiting, and audit logging without passwords.
- Optional Windows Event Log provider, configurable audit text log file, and CAPTCHA configuration placeholder.
- MSTest unit tests for validation support services and password-policy error mapping. No live AD is required for these tests.

## Configuration

Edit `src/DirectorySelfService/appsettings.json` or provide environment variable overrides. ASP.NET Core maps nested options with double underscores, for example:

```powershell
$env:Directory__LdapServer = "dc01.corp.example.com"
$env:Directory__SearchBaseDn = "DC=corp,DC=example,DC=com"
$env:Audit__UsernameHashSalt = "use-a-long-random-secret"
$env:Hosting__DataProtectionKeysPath = "C:\ProgramData\DirectorySelfService\DataProtectionKeys"
```

Important settings:

```json
{
  "Branding": {
    "Title": "Password Self-Service",
    "SupportContact": "IT Service Desk"
  },
  "Directory": {
    "DefaultDomain": "CONTOSO",
    "LdapServer": "dc01.contoso.local",
    "LdapPort": 389,
    "UseSsl": false,
    "UseSigning": true,
    "UseSealing": true,
    "SearchBaseDn": "DC=contoso,DC=local",
    "AllowedGroups": [],
    "RestrictedGroups": [ "Domain Admins", "Enterprise Admins", "Schema Admins" ],
    "LdapTimeoutSeconds": 15
  },
  "Hosting": {
    "HttpsPort": 443,
    "DataProtectionKeysPath": "C:\\ProgramData\\DirectorySelfService\\DataProtectionKeys",
    "DataProtectionApplicationName": "DirectorySelfService"
  },
  "RateLimit": {
    "PermitLimit": 5,
    "WindowMinutes": 15,
    "UsernamePermitLimit": 5
  },
  "Audit": {
    "HashUsernames": true,
    "UsernameHashSalt": "replace-with-random-secret-salt",
    "EnableWindowsEventLog": false,
    "EventLogSource": "DirectorySelfService",
    "TextLogPath": "C:\\ProgramData\\DirectorySelfService\\Logs\\audit.log"
  },
  "Captcha": {
    "Enabled": false,
    "Provider": "",
    "SiteKey": ""
  },
  "AdminPortal": {
    "Enabled": false,
    "SharedSecret": "",
    "WritableSettingsPath": "appsettings.json"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "System.DirectoryServices.Protocols": "Warning",
      "DirectorySelfService.Services.ActiveDirectoryPasswordService": "Information"
    }
  }
}
```


## Admin settings portal

The `/Admin` page can update the JSON settings used for Active Directory communication without editing the file by hand. It is disabled by default. To enable it, set:

```json
"AdminPortal": {
  "Enabled": true,
  "SharedSecret": "replace-with-a-long-random-admin-secret",
  "WritableSettingsPath": "appsettings.json"
}
```

The admin page requires the shared secret on every save and never writes that secret back to the JSON file. It can update:

- `Directory:DefaultDomain`, `Directory:LdapServer`, `Directory:LdapPort`, `Directory:UseSsl`, `Directory:UseSigning`, `Directory:UseSealing`, `Directory:SearchBaseDn`, and `Directory:LdapTimeoutSeconds`.
- `Directory:AllowedGroups` and `Directory:RestrictedGroups`; enter one group common name or distinguished name per line.
- logging levels for the AD password-change service and `System.DirectoryServices.Protocols` so troubleshooting can temporarily use Debug/Trace verbosity. Disable verbose logging after troubleshooting because directory diagnostics can be noisy.

The app pool identity must be able to write `AdminPortal:WritableSettingsPath` if you use the portal to save settings. Environment variables and IIS Configuration Editor values can override JSON; if those overrides exist, either update them too or recycle/restart after removing the override.

## Production hosting settings

The app stores ASP.NET Core Data Protection keys in `Hosting:DataProtectionKeysPath`. Keep this folder outside the publish directory and grant the IIS application pool identity read/write access. Persistent keys allow antiforgery cookies that were issued before an app restart, recycle, or redeploy to be decrypted afterwards. If the folder is deleted or a different path/application name is used, existing browser antiforgery cookies become invalid and users may need to refresh the form.

`Hosting:HttpsPort` configures the target port for HTTPS redirects. Set it to the external HTTPS port used by IIS, or set it to `null` only when HTTPS redirection is handled entirely before requests reach ASP.NET Core.

## Active Directory behavior and permissions

The portal is intended to change passwords as the requesting user, not to perform an administrative password reset. The app validates the supplied current password by binding to LDAP with the submitted UPN or `DOMAIN\\username` identity. After a successful bind, it locates the user under `Directory:SearchBaseDn` and sends a `unicodePwd` delete/add operation with the old and new password values.

LDAPS is disabled by default for this deployment profile. The default connection uses LDAP port `389` with Negotiate authentication plus LDAP signing and sealing (`Directory:UseSigning` and `Directory:UseSealing`) to provide the encrypted channel Active Directory requires for `unicodePwd` changes. If your domain policy requires LDAPS instead, set `Directory:UseSsl` to `true` and use port `636`.

The IIS application pool identity does not need delegated reset-password rights for the normal flow. It does need permission to run the web app and read its files. Directory operations are performed after binding with the user's provided credentials.

Use `AllowedGroups` to restrict self-service to members of specific AD groups. Use `RestrictedGroups` for high-risk groups that should never use this portal, such as domain administrators.

## Security notes

- Host only over HTTPS. The app enables HTTPS redirection and HSTS outside Development, but IIS should also require TLS.
- Do not log request bodies. The app's structured audit logs include timestamp, username hash or normalized username, source IP, result category, and success flag only.
- Replace `Audit:UsernameHashSalt` with a long random value before production use.
- Set `Audit:TextLogPath` to the audit text file location that should receive password-change attempts in addition to the configured logging providers. Leave it empty to disable text-file audit logging. Grant the IIS application pool identity permission to create and append to the folder.
- Keep `RestrictedGroups` populated for privileged accounts.
- Rate limiting is in-memory and suitable for a single-node internal deployment. Use a shared limiter if you deploy multiple instances.
- CAPTCHA settings are placeholders so an enterprise-approved provider can be integrated without changing the form contract.

## Build and test

Install the .NET 8 SDK or newer, then run:

```powershell
dotnet restore
dotnet build DirectorySelfService.sln -c Release
dotnet test DirectorySelfService.sln -c Release
```

## Publish for IIS

1. Install the .NET 8 Hosting Bundle on the IIS server.
2. Publish the app:

   ```powershell
   dotnet publish .\src\DirectorySelfService\DirectorySelfService.csproj -c Release -o C:\inetpub\DirectorySelfService
   ```

3. Create or update the IIS site with the helper script:

   ```powershell
   .\deployment\Install-IisSite.ps1 `
     -SiteName "DirectorySelfService" `
     -PhysicalPath "C:\inetpub\DirectorySelfService" `
     -HostHeader "passwords.corp.example.com" `
     -CertificateThumbprint "YOUR_CERT_THUMBPRINT"
   ```

4. Configure application settings through `appsettings.json`, environment variables, or IIS Configuration Editor.
5. Confirm the site is reachable only over HTTPS.
6. Test with a non-privileged pilot account before production rollout.

## Troubleshooting

- **Invalid current password**: Confirm the user entered the correct UPN or `DOMAIN\\username` and current password.
- **User not found**: Verify `Directory:SearchBaseDn` includes the user object and that UPN or sAMAccountName formats are correct.
- **Antiforgery token could not be decrypted**: Confirm `Hosting:DataProtectionKeysPath` points to a persistent folder that is not removed during publish and that the IIS application pool identity can read/write it. Refreshing the page clears stale browser tokens after the key store is fixed.
- **Failed to determine the https port for redirect**: Set `Hosting:HttpsPort` or the `ASPNETCORE_HTTPS_PORT` environment variable to the IIS HTTPS port.
- **Directory unavailable / LDAP error code 81**: Confirm `Directory:LdapServer` is a reachable domain controller DNS name, firewall access to `Directory:LdapPort` is open from the web server, and LDAP signing/sealing is allowed when `Directory:UseSsl` is `false`. Use `/Admin` to temporarily enable verbose directory logging while troubleshooting.
- **Password policy failure**: Review domain password complexity, history, length, and minimum-age settings. The UI intentionally shows friendly messages instead of raw AD diagnostics.
- **Encrypted LDAP failures**: With LDAPS disabled, keep `Directory:UseSigning` and `Directory:UseSealing` enabled so Negotiate binds can protect LDAP traffic on port `389`. If you re-enable LDAPS, verify the domain controller certificate is valid, trusted by the IIS server, and has the correct DNS name. The password-change service logs LDAP server, port, SSL usage, signing/sealing usage, search base, result codes, and group membership counts at Debug/Trace levels without logging passwords.
- **Windows Event Log not receiving entries**: Enable `Audit:EnableWindowsEventLog`, create/register the event source if required by policy, and ensure the app pool identity has permission to write events.
- **Audit text log not receiving entries**: Confirm `Audit:TextLogPath` is set to the expected file path and that the IIS application pool identity can create the folder and append to the file.

## Clean-room statement

This codebase is an original implementation for the requested business purpose. It avoids third-party SSPR source code, UI, structure, names, text, assets, and implementation patterns.
