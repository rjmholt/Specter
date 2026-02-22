param(
    [Parameter()]
    [ValidateSet('Release', 'Debug')]
    [string]
    $Configuration = 'Debug',

    [Parameter()]
    [ValidateSet('net8', 'net462')]
    [string[]]
    $TargetFramework = $(if ($IsWindows -eq $false) { 'net8' } else { 'net8', 'net462' })
)

$ErrorActionPreference = 'Stop'

$moduleName = "PSpecter"
$outLocation = "$PSScriptRoot/out"
$moduleOutPath = "$outLocation/$moduleName"

if (Test-Path $moduleOutPath)
{
    Remove-Item -Recurse -Force $moduleOutPath
}

Push-Location $PSScriptRoot
try
{
    foreach ($framework in $TargetFramework)
    {
        dotnet publish -f $framework

        if ($LASTEXITCODE -ne 0)
        {
            throw 'Dotnet publish failed'
        }

        New-Item -ItemType Directory -Path "$moduleOutPath/$framework"
        Copy-Item -Path "$PSScriptRoot/bin/$Configuration/$framework/publish/*.dll" -Destination "$moduleOutPath/$framework"
        Copy-Item -Path "$PSScriptRoot/bin/$Configuration/$framework/publish/*.pdb" -Destination "$moduleOutPath/$framework" -ErrorAction Ignore
    }
}
finally
{
    Pop-Location
}

Copy-Item -Path "$PSScriptRoot/$moduleName.psd1" -Destination $moduleOutPath

# Copy database if present
$dataDir = "$PSScriptRoot/Data"
if (Test-Path "$dataDir/pspecter.db") {
    $moduleDataDir = "$moduleOutPath/Data"
    if (-not (Test-Path $moduleDataDir)) {
        New-Item -ItemType Directory -Path $moduleDataDir | Out-Null
    }
    Copy-Item -Path "$dataDir/pspecter.db" -Destination $moduleDataDir
}

# Copy legacy settings for backward compatibility
$settingsSource = "$PSScriptRoot/../Engine/Settings"
if (Test-Path $settingsSource) {
    Copy-Item -Recurse -Path $settingsSource -Destination "$moduleOutPath/Settings" -ErrorAction SilentlyContinue
}