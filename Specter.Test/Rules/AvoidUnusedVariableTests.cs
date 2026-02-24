using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUnusedVariableTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUnusedVariableTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUnusedVariable>())
                .Build();
        }

        [Fact]
        public void AssignedNeverRead_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    $x = 1
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUnusedVariable", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("x", violation.Message);
        }

        [Fact]
        public void AssignedAndRead_ShouldNotReturnViolation()
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
        public void NullDiscard_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $null = Get-Process
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScopeQualifiedVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $script:x = 1
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SpecialVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $ErrorActionPreference = 'Stop'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ReadInNestedScriptBlock_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $x = 'hello'
    Get-Process | ForEach-Object { Write-Host $x }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void MultipleUnused_ShouldReturnMultipleViolations()
        {
            var script = @"
function Foo {
    $x = 1
    $y = 2
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void SplattedVariable_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $params = @{ Name = 'test' }
    Get-Process @params
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
