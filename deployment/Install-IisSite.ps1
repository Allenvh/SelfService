[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $SiteName,
    [Parameter(Mandatory)] [string] $PhysicalPath,
    [string] $AppPoolName = $SiteName,
    [int] $HttpsPort = 443,
    [string] $HostHeader = "",
    [string] $CertificateThumbprint = "",
    [string] $DataProtectionKeysPath = "C:\ProgramData\DirectorySelfService\DataProtectionKeys"
)

Import-Module WebAdministration -ErrorAction Stop

if (-not (Test-Path $PhysicalPath)) {
    throw "Physical path '$PhysicalPath' does not exist. Publish the application before running this script."
}

if ($PSCmdlet.ShouldProcess($DataProtectionKeysPath, "Create Data Protection key directory")) {
    New-Item -ItemType Directory -Path $DataProtectionKeysPath -Force | Out-Null
}

if ($PSCmdlet.ShouldProcess($AppPoolName, "Create or update IIS application pool")) {
    if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
    }
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value ApplicationPoolIdentity
}

if ($PSCmdlet.ShouldProcess($DataProtectionKeysPath, "Grant app pool access to Data Protection keys")) {
    $appPoolIdentity = "IIS AppPool\$AppPoolName"
    & icacls $DataProtectionKeysPath /grant "${appPoolIdentity}:(OI)(CI)(M)" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant '$appPoolIdentity' access to '$DataProtectionKeysPath'."
    }
}

if ($PSCmdlet.ShouldProcess($SiteName, "Create or update IIS site")) {
    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName -Port 80 -HostHeader $HostHeader | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }
}

if ($CertificateThumbprint) {
    $bindingInfo = if ($HostHeader) { "*:$( $HttpsPort ):$HostHeader" } else { "*:$( $HttpsPort ):" }
    if (-not (Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostHeader | Out-Null
    }
    Push-Location IIS:\SslBindings
    try {
        if (-not (Test-Path $bindingInfo)) {
            Get-Item "Cert:\LocalMachine\My\$CertificateThumbprint" | New-Item $bindingInfo | Out-Null
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "IIS site '$SiteName' is configured. Ensure HTTPS is required, appsettings.json contains production directory settings, and Hosting:DataProtectionKeysPath is '$DataProtectionKeysPath'."
