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
    public class UseCorrectCasingTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseCorrectCasingTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/UseCorrectCasing", new UseCorrectCasingEditorConfiguration { Common = new CommonEditorConfiguration { Enable = true } } },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<UseCorrectCasing>())
                .Build();
        }

        [Fact]
        public void CorrectCasing_ShouldNotReturnViolation()
        {
            var script = "Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "UseCorrectCasing").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SimpleScript_ShouldNotReturnViolation()
        {
            var script = "$x = 1 + 2";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "UseCorrectCasing").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void DisabledByDefault()
        {
            var defaultAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseCorrectCasing>())
                .Build();

            var script = "get-process";

            IReadOnlyList<ScriptDiagnostic> violations = defaultAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "UseCorrectCasing").ToList();

            Assert.Empty(violations);
        }
    }
}
