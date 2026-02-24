<#
.SYNOPSIS
    Publishes Specter modules to the PowerShell Gallery and/or NuGet.

.DESCRIPTION
    Runs build.ps1 -Configuration Release -Pack, then publishes:
      - PowerShell modules to the PowerShell Gallery
      - NuGet packages to nuget.org

    Requires API keys passed as parameters or set as environment variables.

.PARAMETER PSGalleryApiKey
    API key for the PowerShell Gallery. Falls back to $env:PSGALLERY_API_KEY.

.PARAMETER NuGetApiKey
    API key for nuget.org. Falls back to $env:NUGET_API_KEY.

.PARAMETER SkipPSGallery
    Skip publishing to the PowerShell Gallery.

.PARAMETER SkipNuGet
    Skip publishing to nuget.org.

.PARAMETER WhatIf
    Show what would be published without actually publishing.

.EXAMPLE
    ./publish.ps1 -PSGalleryApiKey $key
    ./publish.ps1 -NuGetApiKey $key -SkipPSGallery
    ./publish.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$PSGalleryApiKey = $env:PSGALLERY_API_KEY,

    [string]$NuGetApiKey = $env:NUGET_API_KEY,

    [switch]$SkipPSGallery,

    [switch]$SkipNuGet
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$outRoot  = Join-Path $repoRoot 'out'
$nupkgDir = Join-Path $outRoot 'nupkg'

# --- validate ---

if (-not $SkipPSGallery -and [string]::IsNullOrWhiteSpace($PSGalleryApiKey)) {
    throw 'PSGallery API key is required. Pass -PSGalleryApiKey or set $env:PSGALLERY_API_KEY. Use -SkipPSGallery to skip.'
}

if (-not $SkipNuGet -and [string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    throw 'NuGet API key is required. Pass -NuGetApiKey or set $env:NUGET_API_KEY. Use -SkipNuGet to skip.'
}

# --- build ---

Write-Host 'Running release build...' -ForegroundColor Cyan
& "$repoRoot/build.ps1" -Configuration Release -Pack -Clean

if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

# --- run tests ---

Write-Host 'Running tests...' -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot 'Specter.Test/Specter.Test.csproj') --no-restore -c Release -p:NuGetAudit=false

if ($LASTEXITCODE -ne 0) {
    throw 'Tests failed. Aborting publish.'
}

# --- read version for display ---

$manifest = Import-PowerShellDataFile (Join-Path $outRoot 'Specter/Specter.psd1')
$moduleVersion = $manifest.ModuleVersion
$prerelease = $manifest.PrivateData.PSData.Prerelease
$fullVersion = if ($prerelease) { "$moduleVersion-$prerelease" } else { $moduleVersion }

Write-Host "`nPublishing version: $fullVersion" -ForegroundColor Yellow

# --- publish to PSGallery ---

if (-not $SkipPSGallery) {
    $psModules = @(
        @{ Name = 'Specter';                  Path = Join-Path $outRoot 'Specter' }
        @{ Name = 'Specter.PssaCompatibility'; Path = Join-Path $outRoot 'Specter.PssaCompatibility' }
        @{ Name = 'Specter.RuleCmdlets';    Path = Join-Path $outRoot 'Specter.RuleCmdlets' }
    )

    foreach ($mod in $psModules) {
        if (-not (Test-Path $mod.Path)) {
            Write-Warning "Module not found at $($mod.Path), skipping."
            continue
        }

        $manifestFile = Get-ChildItem -Path $mod.Path -Filter '*.psd1' | Select-Object -First 1
        if (-not $manifestFile) {
            Write-Warning "No manifest found in $($mod.Path), skipping."
            continue
        }

        Write-Host "Publishing $($mod.Name) to PSGallery..." -ForegroundColor Cyan

        if ($PSCmdlet.ShouldProcess($mod.Name, 'Publish-Module to PSGallery')) {
            Publish-Module -Path $mod.Path -NuGetApiKey $PSGalleryApiKey -Repository PSGallery
            Write-Host "  Published $($mod.Name) $fullVersion" -ForegroundColor Green
        }
    }
}

# --- publish to NuGet ---

if (-not $SkipNuGet) {
    $packages = Get-ChildItem -Path $nupkgDir -Filter '*.nupkg' -ErrorAction SilentlyContinue

    if (-not $packages) {
        Write-Warning "No .nupkg files found in $nupkgDir."
    }

    foreach ($pkg in $packages) {
        Write-Host "Publishing $($pkg.Name) to nuget.org..." -ForegroundColor Cyan

        if ($PSCmdlet.ShouldProcess($pkg.Name, 'dotnet nuget push')) {
            dotnet nuget push $pkg.FullName --api-key $NuGetApiKey --source https://api.nuget.org/v3/index.json --skip-duplicate

            if ($LASTEXITCODE -ne 0) {
                throw "Failed to push $($pkg.Name)"
            }

            Write-Host "  Published $($pkg.Name)" -ForegroundColor Green
        }
    }
}

# --- done ---

Write-Host "`nPublish complete." -ForegroundColor Green
