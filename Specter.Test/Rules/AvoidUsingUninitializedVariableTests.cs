using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingUninitializedVariableTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingUninitializedVariableTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingUninitializedVariable>())
                .Build();
        }

        [Fact]
        public void ReadBeforeAssign_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    Write-Host $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingUninitializedVariable", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("x", violation.Message);
        }

        [Fact]
        public void AssignedThenRead_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $x = 1
    Write-Host $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ParameterVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    param($x)
    Write-Host $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ForeachVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    foreach ($item in 1..10) {
        Write-Host $item
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void AutomaticVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    Write-Host $_
    Write-Host $PSCmdlet
    Write-Host $PSBoundParameters
    Write-Host $args
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PreferenceVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    Write-Host $ErrorActionPreference
    Write-Host $VerbosePreference
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScopeQualifiedVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    Write-Host $global:config
    Write-Host $script:data
    Write-Host $env:PATH
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void TypedVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    [string]$x = 'hello'
    Write-Host $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void AssignedLaterInScope_ShouldNotReturnViolation()
        {
            // Conservative: assignment exists in scope, even if after the read
            var script = @"
function Foo {
    Write-Host $x
    $x = 1
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void MultipleUninitializedVariables_ReportsOncePerName()
        {
            var script = @"
function Foo {
    Write-Host $x
    Write-Host $x
    Write-Host $y
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
            Assert.Contains(violations, v => v.Message.Contains("x"));
            Assert.Contains(violations, v => v.Message.Contains("y"));
        }

        [Fact]
        public void NullVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    if ($null -eq $x) { 'is null' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            // $null is a special variable, should not be flagged.
            // $x is uninitialized and should be flagged.
            var nullViolations = violations.Where(v => v.Message.Contains("null")).ToList();
            Assert.Empty(nullViolations);
        }
    }
}
