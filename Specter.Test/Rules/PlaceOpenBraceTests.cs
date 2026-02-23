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
    public class PlaceOpenBraceTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PlaceOpenBraceTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/PlaceOpenBrace", new PlaceOpenBraceEditorConfiguration { Common = new CommonEditorConfiguration { Enable = true }, OnSameLine = true } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<PlaceOpenBrace>())
                .Build();
        }

        [Fact]
        public void OpenBraceOnNewLine_WhenSameLineStyleConfigured_ShouldReturnViolation()
        {
            var script = @"
if ($x)
{
    'a'
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PlaceOpenBrace", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void OpenBraceOnSameLine_ShouldNotReturnViolation()
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
