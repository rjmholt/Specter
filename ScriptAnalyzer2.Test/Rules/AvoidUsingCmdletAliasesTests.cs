using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Xunit;

namespace ScriptAnalyzer2.Test.Rules
{
    public class AvoidUsingCmdletAliasesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingCmdletAliasesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingCmdletAliases, AvoidUsingCmdletAliasesConfiguration>(
                    new AvoidUsingCmdletAliasesConfiguration()
                    {
                        AllowList = []
                    }))
                .Build();
        }

        [Fact]
        public void ClearHostAlias_ShouldReturnViolation()
        {
            var script = @"cls";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingCmdletAliases", oneViolation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Warning, oneViolation.Severity);
            Assert.Contains("cls", oneViolation.Message);
            Assert.Contains("Clear-Host", oneViolation.Message);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(4, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void InvokeExpressionAlias_ShouldReturnViolation()
        {
            var script = @"iex 'Get-Process'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingCmdletAliases", oneViolation.Rule.Name);
            Assert.Contains("iex", oneViolation.Message);
            Assert.Contains("Invoke-Expression", oneViolation.Message);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(4, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void GetChildItemAlias_ShouldReturnViolation()
        {
            var script = @"gci -Path C:\";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Contains("gci", oneViolation.Message);
            Assert.Contains("Get-ChildItem", oneViolation.Message);
            Assert.Equal("gci", oneViolation.ScriptExtent.Text);
        }

        [Fact]
        public void MultipleAliases_ShouldReturnMultipleViolations()
        {
            var script = @"
cls
gci
dir";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(3, violations.Count);

            // First violation - cls
            Assert.Equal(2, violations[0].ScriptExtent.StartLineNumber);
            Assert.Equal("cls", violations[0].ScriptExtent.Text);
            Assert.Contains("Clear-Host", violations[0].Message);

            // Second violation - gci
            Assert.Equal(3, violations[1].ScriptExtent.StartLineNumber);
            Assert.Equal("gci", violations[1].ScriptExtent.Text);
            Assert.Contains("Get-ChildItem", violations[1].Message);

            // Third violation - dir
            Assert.Equal(4, violations[2].ScriptExtent.StartLineNumber);
            Assert.Equal("dir", violations[2].ScriptExtent.Text);
            Assert.Contains("Get-ChildItem", violations[2].Message);
        }

        [Fact]
        public void AliasInFunction_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    cls
    gci -Path C:\
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);

            // cls violation
            Assert.Equal(3, violations[0].ScriptExtent.StartLineNumber);
            Assert.Equal(5, violations[0].ScriptExtent.StartColumnNumber);
            Assert.Equal(8, violations[0].ScriptExtent.EndColumnNumber);

            // gci violation
            Assert.Equal(4, violations[1].ScriptExtent.StartLineNumber);
            Assert.Equal(5, violations[1].ScriptExtent.StartColumnNumber);
            Assert.Equal(8, violations[1].ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void AliasInScriptBlock_ShouldReturnViolation()
        {
            var script = @"
Invoke-Command -ScriptBlock {
    cls
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(3, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(5, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(8, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void FullCmdletNames_ShouldNotReturnViolation()
        {
            var script = @"
Clear-Host
Get-ChildItem -Path C:\
Invoke-Expression 'Get-Process'
Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ImplicitGetPrefix_ShouldReturnViolation()
        {
            var script = @"Item";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Contains("Item", oneViolation.Message);
            Assert.Contains("Get-Item", oneViolation.Message);
            Assert.Equal("Item", oneViolation.ScriptExtent.Text);
        }

        [Fact]
        public void AliasWithParameters_ShouldReturnViolation()
        {
            var script = @"gci -Path C:\ -Recurse -Force";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("gci", oneViolation.ScriptExtent.Text);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(4, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void AliasInPipeline_ShouldReturnViolation()
        {
            var script = @"gci | Where-Object {$_.Name -like '*.txt'}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("gci", oneViolation.ScriptExtent.Text);
        }

        [Fact]
        public void CommonAliases_ShouldReturnViolations()
        {
            var script = @"
cd C:\
copy file1.txt file2.txt
del file.txt
echo 'Hello World'
pwd";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(5, violations.Count);
            Assert.Contains(violations, v => v.ScriptExtent.Text == "cd" && v.Message.Contains("Set-Location"));
            Assert.Contains(violations, v => v.ScriptExtent.Text == "copy" && v.Message.Contains("Copy-Item"));
            Assert.Contains(violations, v => v.ScriptExtent.Text == "del" && v.Message.Contains("Remove-Item"));
            Assert.Contains(violations, v => v.ScriptExtent.Text == "echo" && v.Message.Contains("Write-Output"));
            Assert.Contains(violations, v => v.ScriptExtent.Text == "pwd" && v.Message.Contains("Get-Location"));
        }

        [Fact]
        public void SymbolicAliases_ShouldReturnViolations()
        {
            var script = @"
? {$_.Name -eq 'test'}
% {Write-Host $_}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
            Assert.Contains(violations, v => v.ScriptExtent.Text == "?" && v.Message.Contains("Where-Object"));
            Assert.Contains(violations, v => v.ScriptExtent.Text == "%" && v.Message.Contains("ForEach-Object"));
        }

        [Fact]
        public void CaseInsensitiveAliases_ShouldReturnViolations()
        {
            var script = @"
CLS
GCI
DIR";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(3, violations.Count);
            Assert.All(violations, v => Assert.Equal("AvoidUsingCmdletAliases", v.Rule.Name));
        }

        [Fact]
        public void SuggestedCorrections_ShouldBeProvided()
        {
            var script = @"cls";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Correction correction = Assert.Single(oneViolation.Corrections);

            Assert.Equal("Clear-Host", correction.CorrectionText);
            Assert.Contains("Replace cls with Clear-Host", correction.Description);
        }

        [Fact]
        public void AllowlistConfiguration_ShouldSkipAllowedAliases()
        {
            var configuration = new AvoidUsingCmdletAliasesConfiguration()
            {
                AllowList = ["cd", "cls"]
            };

            var scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingCmdletAliases, AvoidUsingCmdletAliasesConfiguration>(configuration))
                .Build();

            var script = @"
cd C:\
cls
gci";

            IReadOnlyList<ScriptDiagnostic> violations = scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            // Only gci should be flagged, cd and cls should be allowed
            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("gci", oneViolation.ScriptExtent.Text);
        }

        [Fact]
        public void AllowlistCaseInsensitive_ShouldWork()
        {
            var configuration = new AvoidUsingCmdletAliasesConfiguration()
            {
                AllowList = ["cd"]
            };

            var scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingCmdletAliases, AvoidUsingCmdletAliasesConfiguration>(configuration))
                .Build();

            var script = @"CD C:\";

            IReadOnlyList<ScriptDiagnostic> violations = scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}