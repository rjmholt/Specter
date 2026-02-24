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
    public class UseConsistentIndentationTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseConsistentIndentationTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/UseConsistentIndentation", new UseConsistentIndentationEditorConfiguration { Common = new CommonEditorConfiguration { Enable = true }, IndentationSize = 4 } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<UseConsistentIndentation>())
                .Build();
        }

        [Fact]
        public void InconsistentIndentation_ShouldReturnViolation()
        {
            var script = @"
if ($x) {
  'a'
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseConsistentIndentation", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ConsistentIndentation_ShouldNotReturnViolation()
        {
            var script = @"
if ($x) {
    'a'
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
