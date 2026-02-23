using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Editors;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Rules
{
    public class PlaceCloseBraceTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PlaceCloseBraceTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/PlaceCloseBrace", new PlaceCloseBraceEditorConfiguration { Common = new CommonEditorConfiguration { Enable = true } } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<PlaceCloseBrace>())
                .Build();
        }

        [Fact]
        public void ClosingBraceOnSameLineAsContent_ShouldReturnViolation()
        {
            var script = @"
if ($x) {
    'a' }
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PlaceCloseBrace", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ClosingBraceOnOwnLine_ShouldNotReturnViolation()
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
