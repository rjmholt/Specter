#Requires -Version 7.0

[CmdletBinding()]
param(
    [string] $DatabasePath = (Join-Path $PSScriptRoot 'Specter' 'Data' 'specter.db'),
    [switch] $SkipBuild,
    [switch] $SkipSession,
    [string] $CompatibilityProfileDir,
    [switch] $RegisterProfileNames
)

$ErrorActionPreference = 'Stop'

if (-not $SkipBuild) {
    Write-Host 'Building Specter...' -ForegroundColor Cyan
    dotnet build (Join-Path $PSScriptRoot 'Specter' 'Specter.csproj') -c Debug --no-restore -v q
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed'
    }
}

$modulePath = Join-Path $PSScriptRoot 'Specter.Module' 'bin' 'Debug' 'net8' 'Specter.Module.dll'
if (-not (Test-Path $modulePath)) {
    throw "Cannot find Specter module at expected path. Did the build succeed?"
}

Write-Host "Importing module from: $modulePath" -ForegroundColor Cyan
Import-Module $modulePath -Force

$specterDll = Join-Path (Split-Path $modulePath) 'Specter.dll'
if (Test-Path $specterDll) {
    [System.Reflection.Assembly]::LoadFrom((Resolve-Path $specterDll)) | Out-Null
}

if (Test-Path $DatabasePath) {
    Write-Host "Removing existing database: $DatabasePath" -ForegroundColor Yellow
    Remove-Item $DatabasePath -Force
}

$settingsDir = Join-Path $PSScriptRoot 'Specter' 'Settings'

$legacyProfiles = @(
    'desktop-3.0-windows.json'
    'desktop-4.0-windows.json'
    'desktop-5.1.14393.206-windows.json'
    'core-6.1.0-windows.json'
)

foreach ($profile in $legacyProfiles) {
    $profilePath = Join-Path $settingsDir $profile
    if (-not (Test-Path $profilePath)) {
        Write-Warning "Legacy profile not found: $profilePath"
        continue
    }

    Write-Host "Importing legacy profile: $profile" -ForegroundColor Cyan
    Update-SpecterDatabase -DatabasePath $DatabasePath -LegacySettingsPath $profilePath -Verbose
}

if (-not $SkipSession) {
    Write-Host 'Importing current pwsh session (with native commands)...' -ForegroundColor Cyan
    Update-SpecterDatabase -DatabasePath $DatabasePath -FromSession -IncludeNativeCommands -Verbose
}

if ($CompatibilityProfileDir -and (Test-Path $CompatibilityProfileDir)) {
    Write-Host "Importing PSCompatibilityCollector profiles from: $CompatibilityProfileDir" -ForegroundColor Cyan

    $specterAsm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $specterDll))
    $schemaType = $specterAsm.GetType('Specter.CommandDatabase.Sqlite.CommandDatabaseSchema')
    $importerType = $specterAsm.GetType('Specter.CommandDatabase.Import.CompatibilityProfileImporter')

    $sqliteAsm = [System.Reflection.Assembly]::LoadFrom(
        (Resolve-Path (Join-Path (Split-Path $modulePath) 'Microsoft.Data.Sqlite.dll')))
    $connType = $sqliteAsm.GetType('Microsoft.Data.Sqlite.SqliteConnection')

    $conn = [Activator]::CreateInstance($connType, @("Data Source=$DatabasePath"))
    $conn.Open()
    try {
        $schemaType.GetMethod('CreateTables').Invoke($null, @($conn))
        $importMethod = $importerType.GetMethod(
            'ImportDirectory',
            [Type[]]@($connType, [string], [bool]))
        $importMethod.Invoke($null, @(
            $conn,
            (Resolve-Path $CompatibilityProfileDir).Path,
            [bool]$RegisterProfileNames))
    } finally {
        $conn.Close()
        $conn.Dispose()
    }

    $profileCount = (Get-ChildItem "$CompatibilityProfileDir/*.json").Count
    Write-Host "  Imported $profileCount compatibility profiles" -ForegroundColor Green
}

Write-Host 'Checkpointing WAL...' -ForegroundColor Cyan
$checkpointConn = [Microsoft.Data.Sqlite.SqliteConnection]::new("Data Source=$DatabasePath")
$checkpointConn.Open()
try {
    $cmd = $checkpointConn.CreateCommand()
    $cmd.CommandText = 'PRAGMA wal_checkpoint(TRUNCATE);'
    $null = $cmd.ExecuteNonQuery()
} finally {
    $checkpointConn.Close()
    $checkpointConn.Dispose()
}

$dbFile = Get-Item $DatabasePath
$jsonSize = ($legacyProfiles | ForEach-Object {
    $p = Join-Path $settingsDir $_
    if (Test-Path $p) { (Get-Item $p).Length }
}) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

Write-Host ''
Write-Host '=== Database Generation Complete ===' -ForegroundColor Green
Write-Host "  Database:   $DatabasePath"
Write-Host "  DB size:    $([math]::Round($dbFile.Length / 1KB, 1)) KB"
Write-Host "  JSON size:  $([math]::Round($jsonSize / 1KB, 1)) KB ($($legacyProfiles.Count) legacy profiles)"
Write-Host "  Ratio:      $([math]::Round($dbFile.Length / $jsonSize * 100, 1))%"

$db = [Specter.CommandDatabase.Sqlite.SqliteCommandDatabase]::Open($DatabasePath)
try {
    $testCommands = @('Get-ChildItem', 'Get-Process', 'Invoke-Command', 'Get-Date')
    foreach ($cmd in $testCommands) {
        $found = $db.CommandExistsOnPlatform($cmd, $null)
        Write-Host "  Validate $cmd : $found" -ForegroundColor ($found ? 'Green' : 'Red')
    }

    if ($IsMacOS) {
        $found = $db.TryGetCommand('date', $null, [ref]$null)
        Write-Host "  Validate native 'date': $found" -ForegroundColor ($found ? 'Green' : 'Red')
    }
} finally {
    $db.Dispose()
}
