#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the pre-populated PSpecter command database from shipped JSON data.

.DESCRIPTION
    This script creates a SQLite database at PSpecter/Data/pspecter.db by importing
    the legacy Engine/Settings JSON files. Run this after dotnet publish.

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

# Load the built assembly
$framework = if ($PSEdition -eq 'Core') { 'net8' } else { 'net462' }
$publishDir = Join-Path $PSScriptRoot 'bin' 'Debug' $framework 'publish'

if (-not (Test-Path $publishDir)) {
    Write-Host "Publish directory not found at $publishDir, trying to build..."
    Push-Location $PSScriptRoot
    try {
        dotnet publish -f $framework
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    } finally {
        Pop-Location
    }
}

# Load Microsoft.Data.Sqlite and PSpecter assemblies
$sqliteDll = Join-Path $publishDir 'Microsoft.Data.Sqlite.dll'
$pspecterDll = Join-Path $publishDir 'PSpecter.dll'

if (-not (Test-Path $sqliteDll)) { throw "Microsoft.Data.Sqlite.dll not found at $sqliteDll" }
if (-not (Test-Path $pspecterDll)) { throw "PSpecter.dll not found at $pspecterDll" }

# Also need SQLitePCLRaw
$rawBundleDll = Get-ChildItem $publishDir -Filter 'SQLitePCLRaw.bundle*.dll' | Select-Object -First 1
$rawCoreDll = Get-ChildItem $publishDir -Filter 'SQLitePCLRaw.core.dll' | Select-Object -First 1
$rawProviderDll = Get-ChildItem $publishDir -Filter 'SQLitePCLRaw.provider*.dll' | Select-Object -First 1

if ($rawBundleDll) { Add-Type -Path $rawBundleDll.FullName -ErrorAction SilentlyContinue }
if ($rawCoreDll) { Add-Type -Path $rawCoreDll.FullName -ErrorAction SilentlyContinue }
if ($rawProviderDll) { Add-Type -Path $rawProviderDll.FullName -ErrorAction SilentlyContinue }
Add-Type -Path $sqliteDll
Add-Type -Path $pspecterDll

# Initialize SQLitePCL
[SQLitePCL.Batteries_V2]::Init()

Write-Host "Creating database at: $OutputPath"
Write-Host "Importing settings from: $SettingsPath"

$connStr = [Microsoft.Data.Sqlite.SqliteConnectionStringBuilder]::new()
$connStr.DataSource = $OutputPath
$connStr.Mode = [Microsoft.Data.Sqlite.SqliteOpenMode]::ReadWriteCreate

$connection = [Microsoft.Data.Sqlite.SqliteConnection]::new($connStr.ToString())
$connection.Open()

try {
    [PSpecter.Runtime.CommandDatabaseSchema]::CreateTables($connection)

    $writer = [PSpecter.Runtime.CommandDatabaseWriter]::new($connection)
    try {
        $writer.WriteSchemaVersion([PSpecter.Runtime.CommandDatabaseSchema]::SchemaVersion)
    } finally {
        $writer.Dispose()
    }

    [PSpecter.Runtime.Import.LegacySettingsImporter]::ImportDirectory($connection, $SettingsPath)

    # Count what was imported
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM Platform"
    $platformCount = $cmd.ExecuteScalar()

    $cmd.CommandText = "SELECT COUNT(*) FROM Module"
    $moduleCount = $cmd.ExecuteScalar()

    $cmd.CommandText = "SELECT COUNT(*) FROM Command"
    $commandCount = $cmd.ExecuteScalar()

    Write-Host "Done! Imported $platformCount platforms, $moduleCount modules, $commandCount commands."
} finally {
    $connection.Dispose()
}

Write-Host "Database written to: $OutputPath"
