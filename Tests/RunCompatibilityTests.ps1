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
    $TargetFramework = 'net8',

    [Parameter()]
    [switch]
    $Build,

    [Parameter()]
    [switch]
    $Detailed,

    [Parameter()]
    [switch]
    $PassThru
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$compatModulePath = "$repoRoot/ScriptAnalyzer2/out/PSLint.PssaCompatibility"

if ($Build -or -not (Test-Path "$compatModulePath/PSLint.PssaCompatibility.psd1"))
{
    Write-Host "Building PSLint.PssaCompatibility..." -ForegroundColor Cyan
    & "$repoRoot/PSLint.PssaCompatibility/build.ps1" -Configuration $Configuration -TargetFramework $TargetFramework
    Write-Host ""
}

if (-not (Test-Path "$compatModulePath/PSLint.PssaCompatibility.psd1"))
{
    throw "Compatibility module not found at $compatModulePath. Run with -Build or run the compat build first."
}

if (Get-Module PSScriptAnalyzer -ErrorAction SilentlyContinue)
{
    Remove-Module PSScriptAnalyzer -Force
}
if (Get-Module PSLint.PssaCompatibility -ErrorAction SilentlyContinue)
{
    Remove-Module PSLint.PssaCompatibility -Force
}

Import-Module $compatModulePath/PSLint.PssaCompatibility.psd1 -Force

$importedModule = Get-Module PSLint.PssaCompatibility
if (-not $importedModule)
{
    throw "Failed to import PSLint.PssaCompatibility module"
}

Write-Host "Module: $($importedModule.Name) v$($importedModule.Version)" -ForegroundColor Green
Write-Host "Commands: $($importedModule.ExportedCmdlets.Keys -join ', ')" -ForegroundColor Green
Write-Host ""

# --- Run Pester ---

$pesterConfig = New-PesterConfiguration
$pesterConfig.Run.PassThru = $true
$pesterConfig.Output.Verbosity = if ($Detailed) { 'Detailed' } else { 'None' }

if ($TestFile)
{
    $pesterConfig.Run.Path = $TestFile
}
else
{
    $pesterConfig.Run.Path = "$PSScriptRoot/Rules"
}

$result = Invoke-Pester -Configuration $pesterConfig

# --- Summarise ---

$allTests = $result.Tests
$totalPassed = ($allTests | Where-Object Result -eq 'Passed').Count
$totalFailed = ($allTests | Where-Object Result -eq 'Failed').Count
$totalSkipped = ($allTests | Where-Object Result -eq 'Skipped').Count

# Map test files to a short rule name.
# Most files follow the pattern RuleName.tests.ps1.
$byFile = $allTests | Group-Object { Split-Path $_.ScriptBlock.File -Leaf }

$ruleResults = foreach ($group in $byFile)
{
    $file = $group.Name
    $ruleName = $file -replace '\.tests\.ps1$', '' -replace '\.Tests\.ps1$', ''
    $passed = ($group.Group | Where-Object Result -eq 'Passed').Count
    $failed = ($group.Group | Where-Object Result -eq 'Failed').Count
    $skipped = ($group.Group | Where-Object Result -eq 'Skipped').Count
    $total = $group.Group.Count

    [PSCustomObject]@{
        Rule    = $ruleName
        File    = $file
        Passed  = $passed
        Failed  = $failed
        Skipped = $skipped
        Total   = $total
        Failures = ($group.Group | Where-Object Result -eq 'Failed')
    }
}

$ruleResults = $ruleResults | Sort-Object Rule

# Determine which rules are fully passing, partially passing, or fully failing.
$fullyPassing  = $ruleResults | Where-Object { $_.Failed -eq 0 -and $_.Passed -gt 0 }
$partiallyPassing = $ruleResults | Where-Object { $_.Failed -gt 0 -and $_.Passed -gt 0 }
$fullyFailing  = $ruleResults | Where-Object { $_.Failed -gt 0 -and $_.Passed -eq 0 }
$allSkipped    = $ruleResults | Where-Object { $_.Passed -eq 0 -and $_.Failed -eq 0 }

Write-Host ""
Write-Host ("=" * 72) -ForegroundColor DarkGray
Write-Host " COMPATIBILITY TEST SUMMARY" -ForegroundColor White
Write-Host ("=" * 72) -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Total: $($allTests.Count)  |  " -NoNewline
Write-Host "Passed: $totalPassed" -ForegroundColor Green -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "Failed: $totalFailed" -ForegroundColor $(if ($totalFailed -gt 0) { 'Red' } else { 'Green' }) -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "Skipped: $totalSkipped" -ForegroundColor Yellow
Write-Host ""

if ($fullyPassing.Count -gt 0)
{
    Write-Host (" Fully passing ($($fullyPassing.Count) rules)") -ForegroundColor Green
    Write-Host (" " + ("-" * 70)) -ForegroundColor DarkGray
    foreach ($r in $fullyPassing)
    {
        $counts = "$($r.Passed)/$($r.Total)"
        Write-Host "  [PASS] $($r.Rule.PadRight(50)) $counts" -ForegroundColor Green
    }
    Write-Host ""
}

if ($partiallyPassing.Count -gt 0)
{
    Write-Host (" Partially passing ($($partiallyPassing.Count) rules)") -ForegroundColor Yellow
    Write-Host (" " + ("-" * 70)) -ForegroundColor DarkGray
    foreach ($r in $partiallyPassing)
    {
        $counts = "$($r.Passed)/$($r.Total) passed, $($r.Failed) failed"
        Write-Host "  [PART] $($r.Rule.PadRight(50)) $counts" -ForegroundColor Yellow

        foreach ($failure in $r.Failures)
        {
            $name = $failure.ExpandedName
            if ($name.Length -gt 68) { $name = $name.Substring(0, 65) + '...' }
            Write-Host "           $name" -ForegroundColor DarkYellow
        }
    }
    Write-Host ""
}

if ($fullyFailing.Count -gt 0)
{
    Write-Host (" Fully failing ($($fullyFailing.Count) rules)") -ForegroundColor Red
    Write-Host (" " + ("-" * 70)) -ForegroundColor DarkGray
    foreach ($r in $fullyFailing)
    {
        $counts = "0/$($r.Total)"
        Write-Host "  [FAIL] $($r.Rule.PadRight(50)) $counts" -ForegroundColor Red
    }
    Write-Host ""
}

if ($allSkipped.Count -gt 0)
{
    Write-Host (" All skipped ($($allSkipped.Count) rules)") -ForegroundColor DarkGray
    Write-Host (" " + ("-" * 70)) -ForegroundColor DarkGray
    foreach ($r in $allSkipped)
    {
        Write-Host "  [SKIP] $($r.Rule.PadRight(50)) $($r.Total) skipped" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Write-Host ("=" * 72) -ForegroundColor DarkGray
Write-Host ""

if ($PassThru)
{
    $result
}
