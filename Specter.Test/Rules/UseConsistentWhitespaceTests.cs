using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Editors;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseConsistentWhitespaceTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseConsistentWhitespaceTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/UseConsistentWhitespace", new UseConsistentWhitespaceEditorConfiguration { Common = new CommonEditorConfiguration { Enable = true }, CheckOperator = true } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<UseConsistentWhitespace>())
                .Build();
        }

        [Fact]
        public void InconsistentWhitespaceAroundOperator_ShouldReturnViolation()
        {
            var script = @"$a+$b";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseConsistentWhitespace", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ConsistentWhitespaceAroundOperator_ShouldNotReturnViolation()
        {
            var script = @"$a + $b";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
