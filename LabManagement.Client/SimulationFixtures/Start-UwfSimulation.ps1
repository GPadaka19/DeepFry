#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [ValidateSet('Protected', 'Unprotected')]
    [string]$State = 'Unprotected',

    [string]$ClientExecutablePath = (
        Join-Path (Split-Path -Parent $PSScriptRoot) 'LabManagement.Client.exe'
    )
)

$fixtureName = "uwfmgr-current-$($State.ToLowerInvariant()).txt"
$fixturePath = Join-Path $PSScriptRoot $fixtureName
$clientPath = (Resolve-Path -LiteralPath $ClientExecutablePath).Path

if (-not (Test-Path -LiteralPath $fixturePath)) {
    throw "Fixture tidak ditemukan: $fixturePath"
}

$env:DOTNET_ENVIRONMENT = 'Development'
$env:Uwf__SimulationFixturePath = $fixturePath

Write-Host "Menjalankan simulasi UWF $State. Tekan Ctrl+C untuk berhenti."
& $clientPath
