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

$compatModuleName = "Specter.PssaCompatibility"
$outLocation = "$PSScriptRoot/../Specter/out"
$moduleOutPath = "$outLocation/$compatModuleName"

if (Test-Path $moduleOutPath)
{
    Remove-Item -Recurse -Force $moduleOutPath
}

Push-Location $PSScriptRoot
try
{
    foreach ($framework in $TargetFramework)
    {
        dotnet publish -f $framework -c $Configuration

        if ($LASTEXITCODE -ne 0)
        {
            throw 'Dotnet publish failed'
        }

        New-Item -ItemType Directory -Path "$moduleOutPath/$framework" -Force | Out-Null
        Copy-Item -Path "$PSScriptRoot/bin/$Configuration/$framework/publish/*.dll" -Destination "$moduleOutPath/$framework"
        Copy-Item -Path "$PSScriptRoot/bin/$Configuration/$framework/publish/*.pdb" -Destination "$moduleOutPath/$framework" -ErrorAction Ignore

        $settingsSource = "$PSScriptRoot/bin/$Configuration/$framework/publish/Settings"
        if (Test-Path $settingsSource)
        {
            Copy-Item -Recurse -Force -Path $settingsSource -Destination "$moduleOutPath/$framework/Settings"
        }

        $dataSource = "$PSScriptRoot/bin/$Configuration/$framework/publish/Data"
        if (Test-Path $dataSource)
        {
            Copy-Item -Recurse -Force -Path $dataSource -Destination "$moduleOutPath/$framework/Data"
        }

        $runtimesSource = "$PSScriptRoot/bin/$Configuration/$framework/publish/runtimes"
        if (Test-Path $runtimesSource)
        {
            Copy-Item -Recurse -Force -Path $runtimesSource -Destination "$moduleOutPath/$framework/runtimes"
        }
    }
}
finally
{
    Pop-Location
}

Copy-Item -Path "$PSScriptRoot/$compatModuleName.psd1" -Destination $moduleOutPath
