#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [ValidateSet('Protected', 'Unprotected')]
    [string]$State = 'Unprotected',

    [string]$ClientExecutablePath = ''
)

$projectRoot = Split-Path -Parent $PSScriptRoot
$fixtureName = "uwfmgr-current-$($State.ToLowerInvariant()).txt"
$fixturePath = Join-Path $PSScriptRoot $fixtureName

if ([string]::IsNullOrWhiteSpace($ClientExecutablePath)) {
    $ClientExecutablePath = Join-Path $projectRoot 'bin\Debug\net8.0\LabManagement.Client.exe'
    if (-not (Test-Path -LiteralPath $ClientExecutablePath)) {
        $ClientExecutablePath = Join-Path $projectRoot 'bin\Release\net8.0\LabManagement.Client.exe'
    }
}

if (-not (Test-Path -LiteralPath $ClientExecutablePath)) {
    Write-Host "Executable LabManagement.Client.exe belum ditemukan. Membangun proyek terlebih dahulu..." -ForegroundColor Yellow
    $clientProject = Join-Path $projectRoot 'LabManagement.Client.csproj'
    dotnet build $clientProject -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "Build gagal. Perbaiki error build lalu jalankan ulang skrip ini."
    }
    $ClientExecutablePath = Join-Path $projectRoot 'bin\Debug\net8.0\LabManagement.Client.exe'
}

$clientPath = (Resolve-Path -LiteralPath $ClientExecutablePath).Path

if (-not (Test-Path -LiteralPath $fixturePath)) {
    throw "Fixture tidak ditemukan: $fixturePath"
}

$env:DOTNET_ENVIRONMENT = 'Development'
$env:Uwf__SimulationFixturePath = $fixturePath

Write-Host "Menjalankan simulasi UWF $State. Tekan Ctrl+C untuk berhenti."
& $clientPath
