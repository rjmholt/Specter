using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidReservedParamsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidReservedParamsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidReservedParams>())
                .Build();
        }

        [Fact]
        public void ReservedParameter_ShouldReturnViolation()
        {
            var script = @"
function Test-Func {
    [CmdletBinding()]
    param($Debug)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("ReservedParams", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Error, violation.Severity);
        }

        [Fact]
        public void NormalParameter_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Func {
    [CmdletBinding()]
    param($Name)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
