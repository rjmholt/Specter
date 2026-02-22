#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the pre-populated PSpecter command database from shipped JSON data
    and the current PowerShell session.

.DESCRIPTION
    This script creates a SQLite database at PSpecter/Data/pspecter.db by:
    1. Importing legacy Engine/Settings JSON files (PS 5.1 and Core 6.1 profiles)
    2. Importing commands from the current PowerShell session

    Run this from the repository root or from the PSpecter directory.

.PARAMETER OutputPath
    Path for the output database file. Defaults to PSpecter/Data/pspecter.db.

.PARAMETER SettingsPath
    Path to the Engine/Settings directory containing legacy JSON profiles.
    Defaults to Engine/Settings relative to the repository root.
#>
param(
    [Parameter()]
    [string]$OutputPath,

    [Parameter()]
    [string]$SettingsPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SettingsPath) {
    $SettingsPath = Join-Path $repoRoot 'Engine' 'Settings'
}

if (-not $OutputPath) {
    $dataDir = Join-Path $PSScriptRoot 'Data'
    if (-not (Test-Path $dataDir)) {
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    }
    $OutputPath = Join-Path $dataDir 'pspecter.db'
}

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force
}

# Publish the project if needed
$framework = if ($PSEdition -eq 'Core') { 'net8' } else { 'net462' }
$publishDir = Join-Path $PSScriptRoot 'bin' 'Debug' $framework 'publish'

if (-not (Test-Path $publishDir)) {
    Write-Host "Publish directory not found at $publishDir, trying to build..."
    Push-Location $PSScriptRoot
    try {
        dotnet publish -f $framework -c Debug
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    } finally {
        Pop-Location
    }
}

$pspecterDll = Join-Path $publishDir 'PSpecter.dll'
if (-not (Test-Path $pspecterDll)) { throw "PSpecter.dll not found at $pspecterDll" }

# Copy the native SQLite library next to the managed DLLs so the runtime can find it.
# dotnet publish puts native libs under runtimes/<rid>/native/ which PowerShell's
# module loader doesn't probe.
$rid = if ($IsWindows) {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'win-arm64' } else { 'win-x64' }
} elseif ($IsMacOS) {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' }
} else {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'linux-arm64' } else { 'linux-x64' }
}
$nativeDir = Join-Path $publishDir 'runtimes' $rid 'native'
if (Test-Path $nativeDir) {
    foreach ($nativeLib in Get-ChildItem $nativeDir) {
        $dest = Join-Path $publishDir $nativeLib.Name
        if (-not (Test-Path $dest)) {
            Copy-Item $nativeLib.FullName $dest
        }
    }
}

# Import the module
Import-Module $pspecterDll -Force

Write-Host "Creating database at: $OutputPath"
Write-Host "Importing settings from: $SettingsPath"

# Step 1: Import legacy JSON settings
Update-PSpecterDatabase -DatabasePath $OutputPath -LegacySettingsPath $SettingsPath -Verbose

# Step 2: Import from the current PowerShell session
Write-Host "Importing commands from current session..."
Update-PSpecterDatabase -DatabasePath $OutputPath -FromSession -Verbose

Write-Host "Database written to: $OutputPath"
