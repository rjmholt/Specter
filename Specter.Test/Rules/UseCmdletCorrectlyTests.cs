using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseCmdletCorrectlyTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseCmdletCorrectlyTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().UseBuiltinDatabase().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseCmdletCorrectly>())
                .Build();
        }

        [Fact]
        public void BasicCmdletUsage_ShouldNotReturnViolation()
        {
            var script = @"Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
