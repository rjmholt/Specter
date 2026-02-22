using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidDefaultTrueValueSwitchParameterTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidDefaultTrueValueSwitchParameterTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidDefaultTrueValueSwitchParameter>())
                .Build();
        }

        [Fact]
        public void SwitchDefaultTrue_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [switch]$MySwitch = $true
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidDefaultValueSwitchParameter", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void SwitchParameterFullyQualifiedType_DefaultTrue_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [System.Management.Automation.SwitchParameter]$MySwitch = $true
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void TwoSwitchesDefaultTrue_ShouldReturnTwoViolations()
        {
            var script = @"
function Test-Function {
    param(
        [switch]$Switch1 = $true,
        [System.Management.Automation.SwitchParameter]$Switch2 = $true
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void SwitchWithNoDefault_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [switch]$MySwitch
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SwitchDefaultFalse_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [switch]$MySwitch = $false
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonSwitchDefaultTrue_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [bool]$MyParam = $true
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SwitchDefaultTrue_CaseInsensitive_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [switch]$MySwitch = $True
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NoParameters_ShouldNotReturnViolation()
        {
            var script = @"Write-Host 'Hello'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
