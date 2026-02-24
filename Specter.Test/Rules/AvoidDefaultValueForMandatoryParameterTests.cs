using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
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
            Assert.Equal("AvoidDefaultValueForMandatoryParameter", violation.Rule!.Name);
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
        public void MandatoryEqualsOne_WithDefault_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory=1)]
        [string]$Param1 = 'defaultValue'
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void MandatoryEqualsZero_WithDefault_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory=0)]
        [string]$Param1 = 'val1'
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
