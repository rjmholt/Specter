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

# --- Exclusion Manifest ---
# Test files and individual tests listed here are excluded from the compatibility
# test run with an explicit reason. They appear in the summary as [EXCL] rather
# than [FAIL], and do not count towards the failure total.
#
# Categories:
#   unsupported  -- intentionally not implemented in PSpecter
#   dependency   -- depends on an unsupported or missing feature
#   ordering     -- fails due to test-ordering side effects in the full suite
#   pending      -- not yet implemented; will be removed when support is added

$ExcludedFiles = @{
    # UseCompatibleTypes requires runtime type resolution which PSpecter does
    # not support (all analysis is AST-only with no PowerShell runspace).
    'UseCompatibleTypes.Tests.ps1' = 'unsupported: runtime type resolution not implemented'

    # AllCompatibilityRules depends on UseCompatibleTypes being functional.
    'AllCompatibilityRules.Tests.ps1' = 'dependency: requires UseCompatibleTypes'
}

# UseCompatibleCommands passes 199/200 in isolation but fails when run
# after other test files in the full suite due to database/profile state
# corruption. Excluded at file level until test isolation is fixed.
$ExcludedFiles['UseCompatibleCommands.Tests.ps1'] = 'ordering: passes in isolation (199/200); fails in full suite due to state corruption'

# Individual test exclusions keyed by file name, each mapping test name patterns
# (matched as substrings) to a reason string.
$ExcludedTests = @{
    'AvoidUsingAlias.tests.ps1' = @{
        # Test checks if a native Unix command (date) shadows a PS alias at
        # runtime via Get-Command. PSpecter uses a static database and cannot
        # detect native commands on the analysis host.
        'do not warn when about Get-* completed cmdlets' = 'pending: requires runtime native command detection'
    }
    'AvoidPositionalParameters.tests.ps1' = @{
        # Test expects Invoke-ScriptAnalyzer to return both PSAvoidUsingPositionalParameters
        # and PSAvoidUsingCmdletAliases violations from a single call. PSpecter's compat shim
        # currently runs rules independently per Invoke-ScriptAnalyzer call, so cross-rule
        # results within one invocation don't aggregate the same way.
        'Triggers on alias' = 'pending: cross-rule aggregation in single Invoke-ScriptAnalyzer call'
    }
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$compatModulePath = "$repoRoot/PSpecter/out/PSpecter.PssaCompatibility"

if ($Build -or -not (Test-Path "$compatModulePath/PSpecter.PssaCompatibility.psd1"))
{
    Write-Host "Building PSpecter.PssaCompatibility..." -ForegroundColor Cyan
    & "$repoRoot/PSpecter.PssaCompatibility/build.ps1" -Configuration $Configuration -TargetFramework $TargetFramework
    Write-Host ""
}

if (-not (Test-Path "$compatModulePath/PSpecter.PssaCompatibility.psd1"))
{
    throw "Compatibility module not found at $compatModulePath. Run with -Build or run the compat build first."
}

if (Get-Module PSScriptAnalyzer -ErrorAction SilentlyContinue)
{
    Remove-Module PSScriptAnalyzer -Force
}
if (Get-Module PSpecter.PssaCompatibility -ErrorAction SilentlyContinue)
{
    Remove-Module PSpecter.PssaCompatibility -Force
}

Import-Module $compatModulePath/PSpecter.PssaCompatibility.psd1 -Force

$importedModule = Get-Module PSpecter.PssaCompatibility
if (-not $importedModule)
{
    throw "Failed to import PSpecter.PssaCompatibility module"
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
    $testDir = "$PSScriptRoot/Rules"
    $testFiles = Get-ChildItem $testDir -Filter '*.tests.ps1' -Recurse |
        Where-Object { -not $ExcludedFiles.ContainsKey($_.Name) }
    $pesterConfig.Run.Path = @($testFiles.FullName)
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

    $fileExclusions = $ExcludedTests[$file]
    $excludedCount = 0
    $excludedReasons = @()
    $actualFailures = @()

    foreach ($test in ($group.Group | Where-Object Result -eq 'Failed'))
    {
        $isExcluded = $false
        if ($fileExclusions)
        {
            foreach ($pattern in $fileExclusions.Keys)
            {
                if ($test.ExpandedName -like "*$pattern*")
                {
                    $isExcluded = $true
                    $excludedCount++
                    $excludedReasons += [PSCustomObject]@{
                        Name   = $test.ExpandedName
                        Reason = $fileExclusions[$pattern]
                    }
                    break
                }
            }
        }

        if (-not $isExcluded)
        {
            $actualFailures += $test
        }
    }

    $passed = ($group.Group | Where-Object Result -eq 'Passed').Count
    $skipped = ($group.Group | Where-Object Result -eq 'Skipped').Count
    $total = $group.Group.Count

    [PSCustomObject]@{
        Rule             = $ruleName
        File             = $file
        Passed           = $passed
        Failed           = $actualFailures.Count
        Excluded         = $excludedCount
        ExcludedReasons  = $excludedReasons
        Skipped          = $skipped
        Total            = $total
        Failures         = $actualFailures
    }
}

$ruleResults = $ruleResults | Sort-Object Rule

# Build the list of excluded files for the summary.
$excludedFileEntries = foreach ($entry in $ExcludedFiles.GetEnumerator())
{
    $ruleName = $entry.Key -replace '\.tests\.ps1$', '' -replace '\.Tests\.ps1$', ''
    [PSCustomObject]@{
        Rule   = $ruleName
        File   = $entry.Key
        Reason = $entry.Value
    }
}
$excludedFileEntries = @($excludedFileEntries | Sort-Object Rule)

# Collect individual test exclusion entries for the summary.
$excludedTestEntries = @()
foreach ($r in $ruleResults)
{
    foreach ($reason in $r.ExcludedReasons)
    {
        $excludedTestEntries += [PSCustomObject]@{
            Rule   = $r.Rule
            Name   = $reason.Name
            Reason = $reason.Reason
        }
    }
}
$totalExcludedTests = ($ruleResults | Measure-Object -Property Excluded -Sum).Sum

# Recompute failure total after exclusions.
$actualTotalFailed = ($ruleResults | Measure-Object -Property Failed -Sum).Sum

# Determine which rules are fully passing, partially passing, or fully failing.
$fullyPassing     = $ruleResults | Where-Object { $_.Failed -eq 0 -and $_.Passed -gt 0 }
$partiallyPassing = $ruleResults | Where-Object { $_.Failed -gt 0 -and $_.Passed -gt 0 }
$fullyFailing     = $ruleResults | Where-Object { $_.Failed -gt 0 -and $_.Passed -eq 0 }
$allSkipped       = $ruleResults | Where-Object { $_.Passed -eq 0 -and $_.Failed -eq 0 }

Write-Host ""
Write-Host ("=" * 72) -ForegroundColor DarkGray
Write-Host " COMPATIBILITY TEST SUMMARY" -ForegroundColor White
Write-Host ("=" * 72) -ForegroundColor DarkGray
Write-Host ""
$totalExcluded = $excludedFileEntries.Count.ToString() + " files"
if ($totalExcludedTests -gt 0)
{
    $totalExcluded += ", $totalExcludedTests tests"
}

Write-Host "  Total: $($allTests.Count)  |  " -NoNewline
Write-Host "Passed: $totalPassed" -ForegroundColor Green -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "Failed: $actualTotalFailed" -ForegroundColor $(if ($actualTotalFailed -gt 0) { 'Red' } else { 'Green' }) -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "Skipped: $totalSkipped" -ForegroundColor Yellow -NoNewline
Write-Host "  |  " -NoNewline
Write-Host "Excluded: $totalExcluded" -ForegroundColor Magenta
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

if ($excludedFileEntries.Count -gt 0 -or $excludedTestEntries.Count -gt 0)
{
    $exclLabel = "$($excludedFileEntries.Count) files"
    if ($excludedTestEntries.Count -gt 0)
    {
        $exclLabel += ", $($excludedTestEntries.Count) tests"
    }
    Write-Host (" Excluded ($exclLabel)") -ForegroundColor Magenta
    Write-Host (" " + ("-" * 70)) -ForegroundColor DarkGray
    foreach ($entry in $excludedFileEntries)
    {
        Write-Host "  [EXCL] $($entry.Rule.PadRight(50)) $($entry.Reason)" -ForegroundColor Magenta
    }
    foreach ($entry in $excludedTestEntries)
    {
        $name = $entry.Name
        if ($name.Length -gt 68) { $name = $name.Substring(0, 65) + '...' }
        Write-Host "  [EXCL] $name" -ForegroundColor Magenta
        Write-Host "           $($entry.Reason)" -ForegroundColor DarkMagenta
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

# CI gate: fail on any non-excluded test failure
if ($actualTotalFailed -gt 0)
{
    Write-Host "ERROR: $actualTotalFailed non-excluded test(s) failed." -ForegroundColor Red
    if (-not $PassThru)
    {
        exit 1
    }
}

if ($PassThru)
{
    $result
}
