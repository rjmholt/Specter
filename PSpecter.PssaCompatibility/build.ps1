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

$compatModuleName = "PSpecter.PssaCompatibility"
$outLocation = "$PSScriptRoot/../PSpecter/out"
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
    }
}
finally
{
    Pop-Location
}

Copy-Item -Path "$PSScriptRoot/$compatModuleName.psd1" -Destination $moduleOutPath
