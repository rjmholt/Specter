using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidExclaimOperatorTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidExclaimOperatorTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidExclaimOperator", new AvoidExclaimOperatorConfiguration { Common = new CommonConfiguration(enable: true) } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidExclaimOperator>())
                .Build();
        }

        [Fact]
        public void ExclaimOperator_ShouldReturnViolation()
        {
            var script = @"!$true";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidExclaimOperator", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NotOperator_ShouldNotReturnViolation()
        {
            var script = @"-not $true";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void DisabledByDefault_ShouldNotReturnViolation()
        {
            var defaultAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidExclaimOperator>())
                .Build();

            var script = @"!$true";

            IReadOnlyList<ScriptDiagnostic> violations = defaultAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
