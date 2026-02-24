using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidShouldContinueWithoutForceTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidShouldContinueWithoutForceTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidShouldContinueWithoutForce>())
                .Build();
        }

        [Fact]
        public void ShouldContinueWithoutForce_ShouldReturnViolation()
        {
            var script = @"
function Test-ShouldContinue {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (!$PSCmdlet.ShouldContinue('Question', 'Warning')) { return }
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidShouldContinueWithoutForce", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ShouldContinueWithForce_ShouldNotReturnViolation()
        {
            var script = @"
function Test-ShouldContinue {
    [CmdletBinding(SupportsShouldProcess)]
    param([switch]$Force)
    if (!$PSCmdlet.ShouldContinue('Question', 'Warning')) { return }
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoShouldContinue_ShouldNotReturnViolation()
        {
            var script = "function Get-Data { Get-Process }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
