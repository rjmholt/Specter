using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidLongLinesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidLongLinesTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidLongLines", new AvoidLongLinesConfiguration { Common = new CommonConfiguration(enable: true), MaximumLineLength = 120 } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidLongLines>())
                .Build();
        }

        [Fact]
        public void LineExceedingMaxLength_ShouldReturnViolation()
        {
            var script = new string('x', 121);

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidLongLines", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ShortLine_ShouldNotReturnViolation()
        {
            var script = @"Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void DisabledByDefault_ShouldNotReturnViolation()
        {
            var defaultAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidLongLines>())
                .Build();

            var script = new string('x', 121);

            IReadOnlyList<ScriptDiagnostic> violations = defaultAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
