[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath,

    [Parameter(Mandatory)]
    [string]$SharedSecret,

    [switch]$Start
)

$serviceName = 'LabManagement Client'
$resolvedExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path

try {
    $keyBytes = [Convert]::FromBase64String($SharedSecret)
    if ($keyBytes.Length -lt 32) { throw 'too short' }
}
catch {
    throw 'SharedSecret harus berupa Client Pairing Key valid dari Host.'
}

if (-not $resolvedExecutablePath.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ExecutablePath harus menunjuk ke LabManagement.Client.exe.'
}

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    throw "Service '$serviceName' sudah ada. Hapus atau perbarui service tersebut terlebih dahulu."
}

New-Service `
    -Name $serviceName `
    -DisplayName $serviceName `
    -Description 'LabManagement Client untuk manajemen PC laboratorium.' `
    -BinaryPathName ('"{0}"' -f $resolvedExecutablePath) `
    -StartupType Automatic

$settingsDirectory = Join-Path $env:ProgramData 'LabManagement\Client'
$settingsPath = Join-Path $settingsDirectory 'client-settings.json'
New-Item -ItemType Directory -Force -Path $settingsDirectory | Out-Null
@{ SharedSecret = $SharedSecret } |
    ConvertTo-Json |
    Set-Content -LiteralPath $settingsPath -Encoding UTF8

$settingsAcl = Get-Acl -LiteralPath $settingsPath
$settingsAcl.SetAccessRuleProtection($true, $false)
$settingsAcl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'SYSTEM', 'FullControl', 'Allow')))
$settingsAcl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Administrators', 'FullControl', 'Allow')))
Set-Acl -LiteralPath $settingsPath -AclObject $settingsAcl

& sc.exe failure 'LabManagement Client' reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

if ($Start) {
    Start-Service -Name $serviceName
}

Write-Host "Service '$serviceName' berhasil dipasang."
