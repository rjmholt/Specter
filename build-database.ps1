#Requires -Version 7.0

[CmdletBinding()]
param(
    [string] $DatabasePath = (Join-Path $PSScriptRoot 'Specter' 'Data' 'specter.db'),
    [switch] $SkipBuild,
    [switch] $SkipSession,
    [string] $CompatibilityProfileDir = (Join-Path $PSScriptRoot 'Tests' 'Profiles')
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

foreach ($stalePath in @($DatabasePath, "$DatabasePath-wal", "$DatabasePath-shm")) {
    if (Test-Path $stalePath) {
        Write-Host "Removing existing database artifact: $stalePath" -ForegroundColor Yellow
        Remove-Item $stalePath -Force
    }
}

if (-not (Test-Path $CompatibilityProfileDir)) {
    throw "Compatibility profile directory not found: $CompatibilityProfileDir"
}

$requiredProfiles = @(
    # Windows PowerShell baselines used by compatibility tests.
    'win-8_x64_6.2.9200.0_3.0_x64_4.0.30319.42000_framework.json'
    'win-8_x64_6.3.9600.0_4.0_x64_4.0.30319.42000_framework.json'
    'win-8_x64_10.0.14393.0_5.1.14393.2791_x64_4.0.30319.42000_framework.json'
    'win-8_x64_10.0.17763.0_5.1.17763.316_x64_4.0.30319.42000_framework.json'
    'win-48_x64_10.0.17763.0_5.1.17763.316_x64_4.0.30319.42000_framework.json'

    # PowerShell Core targets used by compatibility tests.
    'win-8_x64_10.0.14393.0_6.2.4_x64_4.0.30319.42000_core.json'
    'win-8_x64_10.0.17763.0_6.2.4_x64_4.0.30319.42000_core.json'
    'win-4_x64_10.0.18362.0_6.2.4_x64_4.0.30319.42000_core.json'
    'win-8_x64_10.0.14393.0_7.0.0_x64_3.1.2_core.json'
    'win-8_x64_10.0.17763.0_7.0.0_x64_3.1.2_core.json'
    'win-4_x64_10.0.18362.0_7.0.0_x64_3.1.2_core.json'
    'ubuntu_x64_18.04_6.2.4_x64_4.0.30319.42000_core.json'
    'ubuntu_x64_18.04_7.0.0_x64_3.1.2_core.json'
)

Write-Host "Importing selected compatibility profiles from: $CompatibilityProfileDir" -ForegroundColor Cyan
foreach ($profileName in $requiredProfiles) {
    $profilePath = Join-Path $CompatibilityProfileDir $profileName
    if (-not (Test-Path $profilePath)) {
        throw "Required compatibility profile missing: $profilePath"
    }

    Write-Host "  -> $profileName" -ForegroundColor DarkCyan
    Update-SpecterDatabase -DatabasePath $DatabasePath -CompatibilityProfilePath $profilePath -Verbose
}

if (-not $SkipSession) {
    Write-Host 'Importing current pwsh session (with native commands)...' -ForegroundColor Cyan
    Update-SpecterDatabase -DatabasePath $DatabasePath -FromSession -IncludeNativeCommands -Verbose
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
$jsonSize = ($requiredProfiles | ForEach-Object {
    $p = Join-Path $CompatibilityProfileDir $_
    if (Test-Path $p) { (Get-Item $p).Length }
}) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

Write-Host ''
Write-Host '=== Database Generation Complete ===' -ForegroundColor Green
Write-Host "  Database:   $DatabasePath"
Write-Host "  DB size:    $([math]::Round($dbFile.Length / 1KB, 1)) KB"
Write-Host "  JSON size:  $([math]::Round($jsonSize / 1KB, 1)) KB ($($requiredProfiles.Count) selected compatibility profiles)"
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
