param(
    [Parameter()]
    [string[]]
    $TestFile,

    [Parameter()]
    [ValidateSet('Release', 'Debug')]
    [string]
    $Configuration = 'Debug',

    [Parameter()]
    [string]
    $TargetFramework = 'net8'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$compatModulePath = "$repoRoot/ScriptAnalyzer2/out/PSLint.PssaCompatibility"

if (-not (Test-Path $compatModulePath))
{
    Write-Host "Building PSLint.PssaCompatibility..." -ForegroundColor Cyan
    & "$repoRoot/PSLint.PssaCompatibility/build.ps1" -Configuration $Configuration -TargetFramework $TargetFramework
}

if (-not (Test-Path "$compatModulePath/PSLint.PssaCompatibility.psd1"))
{
    throw "Compatibility module not found at $compatModulePath. Run the compat build first."
}

# Remove any existing PSScriptAnalyzer to avoid conflicts
if (Get-Module PSScriptAnalyzer -ErrorAction SilentlyContinue)
{
    Remove-Module PSScriptAnalyzer -Force
}
if (Get-Module PSLint.PssaCompatibility -ErrorAction SilentlyContinue)
{
    Remove-Module PSLint.PssaCompatibility -Force
}

Write-Host "Importing PSLint.PssaCompatibility from $compatModulePath" -ForegroundColor Cyan
Import-Module $compatModulePath/PSLint.PssaCompatibility.psd1 -Force

# Verify the module loaded correctly
$importedModule = Get-Module PSLint.PssaCompatibility
if (-not $importedModule)
{
    throw "Failed to import PSLint.PssaCompatibility module"
}

Write-Host "Module loaded: $($importedModule.Name) v$($importedModule.Version)" -ForegroundColor Green
Write-Host "Exported commands: $($importedModule.ExportedCmdlets.Keys -join ', ')" -ForegroundColor Green

$pesterParams = @{
    Output = 'Detailed'
}

if ($TestFile)
{
    $pesterParams.Path = $TestFile
}
else
{
    $pesterParams.Path = "$PSScriptRoot/Rules"
}

$pesterConfig = New-PesterConfiguration -Hashtable $pesterParams
Invoke-Pester -Configuration $pesterConfig
