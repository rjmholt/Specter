using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Xunit;

namespace ScriptAnalyzer2.Test.Rules
{
    public class AvoidDefaultValueForMandatoryParameterTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidDefaultValueForMandatoryParameterTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidDefaultValueForMandatoryParameter>())
                .Build();
        }

        [Fact]
        public void MandatoryWithDefault_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name = 'default'
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidDefaultValueForMandatoryParameter", violation.Rule.Name);
        }

        [Fact]
        public void MandatoryWithoutExplicitTrue_WithDefault_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory)]
        [string]$Name = 'default'
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void MandatoryWithoutDefault_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonMandatoryWithDefault_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter()]
        [string]$Name = 'default'
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void MandatoryFalseWithDefault_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory=$false)]
        [string]$Name = 'default'
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoParamBlock_ShouldNotReturnViolation()
        {
            var script = @"function Test-Function { Write-Host 'Hello' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
