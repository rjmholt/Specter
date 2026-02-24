<#
.SYNOPSIS
    Builds all Specter assemblies and stages the PowerShell modules.

.DESCRIPTION
    Builds each shipping project and stages three PowerShell modules under out/:
      - Specter             (primary module)
      - Specter.PssaCompatibility (drop-in PSSA replacement)
      - Specter.RulePrimitives   (cmdlets for rule authors)

    When -Pack is set, also produces .nupkg files under out/nupkg/:
      - PSGallery-compatible packages for each module (for Install-PSResource / GitHub releases)
      - NuGet library package for Specter (for C# consumers via dotnet add package)

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER TargetFramework
    Target framework(s). Defaults to net8 on non-Windows; net8+net462 on Windows.

.PARAMETER Pack
    Also produce .nupkg files under out/nupkg/.

.PARAMETER Clean
    Remove out/ before building.

.EXAMPLE
    ./build.ps1
    ./build.ps1 -Configuration Release -Pack
    ./build.ps1 -Clean -Pack
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [ValidateSet('net8', 'net462')]
    [string[]]$TargetFramework = $(if ($IsWindows -eq $false) { 'net8' } else { 'net8', 'net462' }),

    [switch]$Pack,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$outRoot    = Join-Path $repoRoot 'out'
$nupkgDir   = Join-Path $outRoot 'nupkg'

# --- helpers ---

function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function New-Dir([string]$Path) {
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Stage-BuildOutput {
    param(
        [string]$Csproj,
        [string]$ModuleOutPath,
        [string]$Framework,
        [string]$Configuration
    )

    Write-Host "  Building $Framework..." -ForegroundColor Cyan
    Invoke-DotNet build $Csproj -f $Framework -c $Configuration --no-restore -p:NuGetAudit=false

    $projectDir = Split-Path $Csproj
    $binDir = Join-Path $projectDir "bin/$Configuration/$Framework"
    $fwDir  = Join-Path $ModuleOutPath $Framework
    New-Dir $fwDir

    Copy-Item -Path "$binDir/*.dll" -Destination $fwDir
    Copy-Item -Path "$binDir/*.pdb" -Destination $fwDir -ErrorAction Ignore

    foreach ($subdir in 'runtimes', 'Data', 'Settings') {
        $src = Join-Path $binDir $subdir
        if (Test-Path $src) {
            Copy-Item -Recurse -Force -Path $src -Destination (Join-Path $fwDir $subdir)
        }
    }
}

# --- clean ---

if ($Clean -and (Test-Path $outRoot)) {
    Write-Host "Cleaning $outRoot..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $outRoot
}

# --- restore ---

Write-Host "Restoring packages..." -ForegroundColor Cyan
Invoke-DotNet restore (Join-Path $repoRoot 'Specter.sln') -p:NuGetAudit=false

# --- build and stage each shipping module ---
#
# We build each shipping project individually with an explicit -f <framework>
# rather than building the whole solution or using dotnet publish. This avoids
# NETSDK1129 (dotnet publish propagates Publish to multi-targeting references)
# and ensures each project is built for each requested framework.
#
# Each entry maps a shipping module to:
#   Csproj       - the .csproj file to build
#   ManifestDir  - where the .psd1 manifest lives
#   Manifest     - the .psd1 filename
#   OutName      - folder name under out/

$projects = @(
    @{
        Csproj      = 'Specter.Module/Specter.Module.csproj'
        ManifestDir = 'Specter.Module'
        Manifest    = 'Specter.psd1'
        OutName     = 'Specter'
    }
    @{
        Csproj      = 'Specter.PssaCompatibility/Specter.PssaCompatibility.csproj'
        ManifestDir = 'Specter.PssaCompatibility'
        Manifest    = 'Specter.PssaCompatibility.psd1'
        OutName     = 'Specter.PssaCompatibility'
    }
    @{
        Csproj      = 'Specter.RulePrimitives/Specter.RulePrimitives.csproj'
        ManifestDir = 'Specter.RulePrimitives'
        Manifest    = 'Specter.RulePrimitives.psd1'
        OutName     = 'Specter.RulePrimitives'
    }
)

foreach ($proj in $projects) {
    $csprojPath    = Join-Path $repoRoot $proj.Csproj
    $moduleOutPath = Join-Path $outRoot $proj.OutName

    if (Test-Path $moduleOutPath) {
        Remove-Item -Recurse -Force $moduleOutPath
    }

    Write-Host "Building $($proj.OutName) [$Configuration]..." -ForegroundColor Cyan
    foreach ($framework in $TargetFramework) {
        Stage-BuildOutput -Csproj $csprojPath -ModuleOutPath $moduleOutPath -Framework $framework -Configuration $Configuration
    }

    # Copy the module manifest
    $manifestSrc = Join-Path (Join-Path $repoRoot $proj.ManifestDir) $proj.Manifest
    if (Test-Path $manifestSrc) {
        Copy-Item -Path $manifestSrc -Destination $moduleOutPath
    }

    # Copy the data directory (Specter module ships specter.db)
    if ($proj.OutName -eq 'Specter') {
        $dataDir = Join-Path $repoRoot 'Specter/Data'
        if (Test-Path "$dataDir/specter.db") {
            $destData = Join-Path $moduleOutPath 'Data'
            New-Dir $destData
            Copy-Item -Path "$dataDir/specter.db" -Destination $destData
        }
    }

    Write-Host "  -> $moduleOutPath" -ForegroundColor Green
}

# --- pack ---

if ($Pack) {
    New-Dir $nupkgDir

    # PSGallery-compatible .nupkg for each PowerShell module (for Install-Module / GitHub releases).
    # Uses a temporary local PSResourceRepository to produce the packages.
    Write-Host "Packing PowerShell module nupkgs..." -ForegroundColor Cyan
    $repoName = "SpecterLocalBuild_$([guid]::NewGuid().ToString('N')[0..7] -join '')"
    try {
        Register-PSResourceRepository -Name $repoName -Uri $nupkgDir -Trusted
        foreach ($proj in $projects) {
            $modulePath = Join-Path $outRoot $proj.OutName
            if (-not (Test-Path $modulePath)) {
                Write-Warning "Module not found at $modulePath, skipping."
                continue
            }
            Write-Host "  Packing $($proj.OutName)..." -ForegroundColor Cyan
            Publish-PSResource -Path $modulePath -Repository $repoName
        }
    }
    finally {
        Unregister-PSResourceRepository -Name $repoName -ErrorAction Ignore
    }

    # NuGet library packages for C# consumers (dotnet add package ...).
    Write-Host "Packing NuGet library packages..." -ForegroundColor Cyan
    $packableProjects = @(
        'Specter.Api/Specter.Api.csproj',
        'Specter.Rules/Specter.Rules.csproj',
        'Specter/Specter.csproj'
    )
    foreach ($csproj in $packableProjects) {
        $fullPath = Join-Path $repoRoot $csproj
        Invoke-DotNet pack $fullPath -c $Configuration -o $nupkgDir --no-restore -p:NuGetAudit=false
    }

    Write-Host "  -> $nupkgDir" -ForegroundColor Green
}

# --- summary ---

Write-Host "`nBuild complete." -ForegroundColor Green
Write-Host "Modules staged in: $outRoot"

if ($Pack) {
    Write-Host "NuGet packages in: $nupkgDir"
    Write-Host "  (attach .nupkg files to GitHub releases for Install-PSResource)"
}

Write-Host "`nTo import the Specter module:"
Write-Host "  Import-Module $outRoot/Specter/Specter.psd1"
