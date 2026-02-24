Describe "External Rule Loading" {

    BeforeAll {
        $fixtureBase = Join-Path $PSScriptRoot '..' 'Specter.Test' 'TestFixtures'
    }

    Context "Module Manifest Audit" {
        It "Accepts a clean PSSA rule module manifest" {
            $manifestPath = Join-Path $fixtureBase 'PssaRuleModule' 'PssaTestRules.psd1'
            $manifestPath | Should -Exist
            $data = Import-PowerShellDataFile $manifestPath
            $data.Keys | Should -Not -Contain 'ScriptsToProcess'
        }

        It "Accepts a legacy DiagnosticRecord module manifest" {
            $manifestPath = Join-Path $fixtureBase 'LegacyDiagnosticRecordModule' 'LegacyTestRules.psd1'
            $manifestPath | Should -Exist
            $data = Import-PowerShellDataFile $manifestPath
            $data.Keys | Should -Not -Contain 'ScriptsToProcess'
            $data.FunctionsToExport | Should -Contain 'Measure-InvokeExpression'
            $data.FunctionsToExport | Should -Contain 'Measure-DangerousMethod'
        }

        It "Accepts a localized rule module manifest" {
            $manifestPath = Join-Path $fixtureBase 'LocalizedRuleModule' 'LocalizedTestRules.psd1'
            $manifestPath | Should -Exist
            $data = Import-PowerShellDataFile $manifestPath
            $data.Keys | Should -Not -Contain 'ScriptsToProcess'
            $data.FunctionsToExport | Should -Contain 'Measure-WriteHost'
        }

        It "Detects dangerous ScriptsToProcess in manifest" {
            $manifestPath = Join-Path $fixtureBase 'DangerousModule' 'Dangerous.psd1'
            $manifestPath | Should -Exist
            $data = Import-PowerShellDataFile $manifestPath
            $data.ScriptsToProcess | Should -Not -BeNullOrEmpty
        }
    }

    Context "PSSA Rule Module Discovery" {
        It "PSSA-convention module exports Measure-* functions" {
            $modulePath = Join-Path $fixtureBase 'PssaRuleModule' 'PssaTestRules.psm1'
            $modulePath | Should -Exist

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $commands = Get-Command -Module $module.Name -CommandType Function
                $measureCommands = $commands | Where-Object { $_.Name -like 'Measure-*' }
                $measureCommands | Should -Not -BeNullOrEmpty
                $measureCommands.Name | Should -Contain 'Measure-EmptyDescription'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }

        It "Legacy DiagnosticRecord module exports Measure-* functions" {
            $modulePath = Join-Path $fixtureBase 'LegacyDiagnosticRecordModule' 'LegacyTestRules.psd1'
            $modulePath | Should -Exist

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $commands = Get-Command -Module $module.Name -CommandType Function
                $measureCommands = $commands | Where-Object { $_.Name -like 'Measure-*' }
                $measureCommands | Should -Not -BeNullOrEmpty
                $measureCommands.Name | Should -Contain 'Measure-InvokeExpression'
                $measureCommands.Name | Should -Contain 'Measure-DangerousMethod'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }

        It "Localized module exports Measure-* functions with Import-LocalizedData" {
            $modulePath = Join-Path $fixtureBase 'LocalizedRuleModule' 'LocalizedTestRules.psd1'
            $modulePath | Should -Exist

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $commands = Get-Command -Module $module.Name -CommandType Function
                $measureCommands = $commands | Where-Object { $_.Name -like 'Measure-*' }
                $measureCommands | Should -Not -BeNullOrEmpty
                $measureCommands.Name | Should -Contain 'Measure-WriteHost'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context "Legacy Rule Execution" {
        BeforeAll {
            # Ensure PssaCompatibility types are available for DiagnosticRecord creation
            $compatPath = Join-Path $PSScriptRoot '..' 'Specter.PssaCompatibility' 'bin' 'Debug' 'net8' 'Specter.PssaCompatibility.dll'
            if (Test-Path $compatPath) {
                Add-Type -Path $compatPath -ErrorAction SilentlyContinue
            }
        }

        It "Measure-InvokeExpression detects Invoke-Expression" {
            $modulePath = Join-Path $fixtureBase 'LegacyDiagnosticRecordModule' 'LegacyTestRules.psd1'
            if (-not (Test-Path $modulePath)) {
                Set-ItResult -Skipped -Because "LegacyDiagnosticRecordModule fixture not found"
                return
            }

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $ast = [System.Management.Automation.Language.Parser]::ParseInput(
                    'Invoke-Expression $command',
                    [ref]$null,
                    [ref]$null
                )
                $result = Measure-InvokeExpression -ScriptBlockAst $ast
                $result | Should -Not -BeNullOrEmpty
                $result.Message | Should -BeLike '*Invoke-Expression*'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }

        It "Measure-DangerousMethod detects InvokeScript (positional binding)" {
            $modulePath = Join-Path $fixtureBase 'LegacyDiagnosticRecordModule' 'LegacyTestRules.psd1'
            if (-not (Test-Path $modulePath)) {
                Set-ItResult -Skipped -Because "LegacyDiagnosticRecordModule fixture not found"
                return
            }

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $ast = [System.Management.Automation.Language.Parser]::ParseInput(
                    '$ps.InvokeScript($code)',
                    [ref]$null,
                    [ref]$null
                )
                # Measure-DangerousMethod uses $testAst (not $ScriptBlockAst), test positional binding
                $result = Measure-DangerousMethod $ast
                $result | Should -Not -BeNullOrEmpty
                $result.Message | Should -BeLike '*InvokeScript*'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }

        It "Measure-WriteHost detects Write-Host with localized message" {
            $modulePath = Join-Path $fixtureBase 'LocalizedRuleModule' 'LocalizedTestRules.psd1'
            if (-not (Test-Path $modulePath)) {
                Set-ItResult -Skipped -Because "LocalizedRuleModule fixture not found"
                return
            }

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $ast = [System.Management.Automation.Language.Parser]::ParseInput(
                    'Write-Host "hello"',
                    [ref]$null,
                    [ref]$null
                )
                $result = Measure-WriteHost -ScriptBlockAst $ast
                $result | Should -Not -BeNullOrEmpty
                $result.Message | Should -BeLike '*Write-Host*'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }

        It "PSSA rule returns no result for clean script" {
            $modulePath = Join-Path $fixtureBase 'LegacyDiagnosticRecordModule' 'LegacyTestRules.psd1'
            if (-not (Test-Path $modulePath)) {
                Set-ItResult -Skipped -Because "LegacyDiagnosticRecordModule fixture not found"
                return
            }

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $ast = [System.Management.Automation.Language.Parser]::ParseInput(
                    'Get-ChildItem | Where-Object { $_.Length -gt 0 }',
                    [ref]$null,
                    [ref]$null
                )
                $result = Measure-InvokeExpression -ScriptBlockAst $ast
                $result | Should -BeNullOrEmpty
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context "RuleCmdlets Module" {
        It "Specter.RuleCmdlets module can be imported" {
            $primitivesPath = Join-Path $PSScriptRoot '..' 'Specter.RuleCmdlets' 'bin' 'Debug' 'net8' 'Specter.RuleCmdlets.psd1'
            if (-not (Test-Path $primitivesPath)) {
                $primitivesPath = Join-Path $PSScriptRoot '..' 'Specter.RuleCmdlets' 'Specter.RuleCmdlets.psd1'
            }
            if (Test-Path $primitivesPath) {
                { Import-Module $primitivesPath -Force -ErrorAction Stop } | Should -Not -Throw
                Get-Command -Module 'Specter.RuleCmdlets' -Name 'Write-Diagnostic' | Should -Not -BeNullOrEmpty
                Get-Command -Module 'Specter.RuleCmdlets' -Name 'New-ScriptCorrection' | Should -Not -BeNullOrEmpty
                Remove-Module 'Specter.RuleCmdlets' -Force -ErrorAction SilentlyContinue
            }
            else {
                Set-ItResult -Skipped -Because "Specter.RuleCmdlets module not found (run dotnet build first)"
            }
        }
    }

    Context "Native Rule Module Discovery" {
        It "Native module exports [SpecterRule] functions" {
            $modulePath = Join-Path $fixtureBase 'NativeRuleModule' 'NativeTestRules.psd1'
            $modulePath | Should -Exist

            $module = Import-Module $modulePath -PassThru -Force
            try {
                $commands = Get-Command -Module $module.Name -CommandType Function
                $commands | Should -Not -BeNullOrEmpty
                $commands.Name | Should -Contain 'Test-NoWriteHost'
                $commands.Name | Should -Contain 'Test-NoHardcodedPasswords'
            }
            finally {
                Remove-Module $module.Name -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context "C# DLL Rule Loading via Invoke-Specter" {
        BeforeAll {
            $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
            $fixtureDir = Join-Path $repoRoot 'Specter.Test' 'TestFixtures' 'SampleCSharpRuleModule'
            $csprojPath = Join-Path $fixtureDir 'SampleCSharpRuleModule.csproj'
            if (-not (Test-Path $csprojPath)) {
                Set-ItResult -Skipped -Because "SampleCSharpRuleModule fixture not found"
            }

            & dotnet build $csprojPath -c Debug -p:NuGetAudit=false | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Set-ItResult -Skipped -Because "Could not build C# fixture DLL"
            }

            $script:ruleDll = Join-Path $fixtureDir 'bin' 'Debug' 'net8.0' 'SampleCSharpRuleModule.dll'
            if (-not (Test-Path $ruleDll)) {
                Set-ItResult -Skipped -Because "C# fixture DLL output not found"
            }

            $moduleManifestCandidates = @(
                (Join-Path $repoRoot 'out' 'Specter' 'Specter.psd1'),
                (Join-Path $repoRoot 'Specter.Module' 'bin' 'Debug' 'net8' 'Specter.psd1'),
                (Join-Path $repoRoot 'Specter.Module' 'Specter.psd1')
            )
            $script:moduleManifest = $moduleManifestCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
            if (-not (Test-Path $moduleManifest)) {
                Set-ItResult -Skipped -Because "Specter module manifest not found (run build.ps1 first)"
            }
        }

        It "Loads an external C# rule DLL and returns diagnostics" {
            Import-Module $moduleManifest -Force
            try {
                $result = Invoke-Specter -ScriptDefinition 'Invoke-Expression $cmd' -CustomRulePath $ruleDll
                $result | Should -Not -BeNullOrEmpty
                ($result | Where-Object Message -Like '*Invoke-Expression*') | Should -Not -BeNullOrEmpty
            }
            finally {
                Remove-Module Specter -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
