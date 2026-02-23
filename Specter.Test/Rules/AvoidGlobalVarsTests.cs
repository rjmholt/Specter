using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidGlobalVarsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidGlobalVarsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidGlobalVars>())
                .Build();
        }

        [Fact]
        public void GlobalVariable_ShouldReturnViolation()
        {
            var script = @"$Global:myVar = 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidGlobalVars", oneViolation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, oneViolation.Severity);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(14, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void GlobalVariableRead_ShouldReturnViolation()
        {
            var script = @"Write-Host $Global:myVar";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(12, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(25, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void MultipleGlobalVariables_ShouldReturnMultipleViolations()
        {
            var script = @"
$Global:var1 = 'test1'
$Global:var2 = 'test2'
Write-Host $Global:var3";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(3, violations.Count);

            // First violation
            Assert.Equal(2, violations[0].ScriptExtent.StartLineNumber);
            Assert.Equal(2, violations[0].ScriptExtent.EndLineNumber);
            Assert.Equal(1, violations[0].ScriptExtent.StartColumnNumber);
            Assert.Equal(13, violations[0].ScriptExtent.EndColumnNumber);

            // Second violation
            Assert.Equal(3, violations[1].ScriptExtent.StartLineNumber);
            Assert.Equal(3, violations[1].ScriptExtent.EndLineNumber);
            Assert.Equal(1, violations[1].ScriptExtent.StartColumnNumber);
            Assert.Equal(13, violations[1].ScriptExtent.EndColumnNumber);

            // Third violation
            Assert.Equal(4, violations[2].ScriptExtent.StartLineNumber);
            Assert.Equal(4, violations[2].ScriptExtent.EndLineNumber);
            Assert.Equal(12, violations[2].ScriptExtent.StartColumnNumber);
            Assert.Equal(24, violations[2].ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void LocalVariable_ShouldNotReturnViolation()
        {
            var script = @"$localVar = 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScriptScopeVariable_ShouldNotReturnViolation()
        {
            var script = @"$Script:myVar = 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PrivateScopeVariable_ShouldNotReturnViolation()
        {
            var script = @"$Private:myVar = 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void GlobalLastExitCode_ShouldNotReturnViolation()
        {
            var script = @"
if ($global:lastexitcode -ne 0) {
    exit
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void GlobalDebugPreference_ShouldNotReturnViolation()
        {
            var script = @"$Global:DebugPreference = 'Continue'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void GlobalVariableInFunction_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    $Global:functionVar = 'test'
    return $Global:functionVar
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);

            // Assignment violation
            Assert.Equal(3, violations[0].ScriptExtent.StartLineNumber);
            Assert.Equal(3, violations[0].ScriptExtent.EndLineNumber);
            Assert.Equal(5, violations[0].ScriptExtent.StartColumnNumber);
            Assert.Equal(24, violations[0].ScriptExtent.EndColumnNumber);

            // Read violation
            Assert.Equal(4, violations[1].ScriptExtent.StartLineNumber);
            Assert.Equal(4, violations[1].ScriptExtent.EndLineNumber);
            Assert.Equal(12, violations[1].ScriptExtent.StartColumnNumber);
            Assert.Equal(31, violations[1].ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void GlobalVariableInScriptBlock_ShouldReturnViolation()
        {
            var script = @"
Invoke-Command -ScriptBlock {
    $Global:blockVar = 'test'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(3, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(3, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(5, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(21, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void MixedScopeVariables_ShouldReturnOnlyGlobalViolations()
        {
            var script = @"
$localVar = 'local'
$Script:scriptVar = 'script'
$Global:globalVar = 'global'
$Private:privateVar = 'private'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Contains("globalVar", oneViolation.Message);
            Assert.Equal(4, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(4, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(18, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void AutomaticVariables_ShouldNotReturnViolation()
        {
            var script = @"
Write-Host $Host
Write-Host $PSVersionTable
Write-Host $matches[0]";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void EmptyScript_ShouldReturnNoViolations()
        {
            var script = @"# Just a comment";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void GlobalVariableWithNumericName_ShouldReturnViolation()
        {
            var script = @"$Global:1 = 'globalVar'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Contains("Global:1", oneViolation.Message);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(10, oneViolation.ScriptExtent.EndColumnNumber);
            Assert.Equal("$Global:1", oneViolation.ScriptExtent.Text);
        }
    }
}
