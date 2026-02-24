using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseUTF8EncodingForHelpFileTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseUTF8EncodingForHelpFileTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseUTF8EncodingForHelpFile>())
                .Build();
        }

        [Fact]
        public void RegularScript_ShouldNotReturnViolation()
        {
            var script = "Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScriptInput_ShouldNotReturnViolation()
        {
            var script = @"
function Get-Foo { }
Write-Output 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
