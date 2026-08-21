[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ExecutablePath,

    [switch]$Start
)

$serviceName = 'LabManagement Client'
$resolvedExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path

if (-not $resolvedExecutablePath.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ExecutablePath harus menunjuk ke LabManagement.Client.exe.'
}

$legacySettingsPath = Join-Path $env:ProgramData 'LabManagement\Client\client-settings.json'
if (Test-Path -LiteralPath $legacySettingsPath) {
    Remove-Item -LiteralPath $legacySettingsPath -Force
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

if (-not (Get-NetFirewallRule -DisplayName 'Deep Fry Client TCP 5020' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
        -DisplayName 'Deep Fry Client TCP 5020' `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort 5020 `
        -RemoteAddress LocalSubnet | Out-Null
}

& sc.exe failure 'LabManagement Client' reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

if ($Start) {
    Start-Service -Name $serviceName
}

Write-Host "Service '$serviceName' berhasil dipasang."
