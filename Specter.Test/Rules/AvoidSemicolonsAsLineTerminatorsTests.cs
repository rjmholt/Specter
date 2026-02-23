using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidSemicolonsAsLineTerminatorsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidSemicolonsAsLineTerminatorsTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidSemicolonsAsLineTerminators", null },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidSemicolonsAsLineTerminators>())
                .Build();
        }

        [Fact]
        public void SemicolonAsLineTerminator_ShouldReturnViolation()
        {
            var script = "Get-Process;";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidSemicolonsAsLineTerminators", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NoSemicolons_ShouldNotReturnViolation()
        {
            var script = "Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
