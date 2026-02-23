using System.Collections.Generic;
using System.Linq;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using Specter;
using Specter.Builder;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseCompatibleCmdletsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseCompatibleCmdletsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().UseBuiltinDatabase().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseCompatibleCmdlets, UseCompatibleCmdletsConfiguration>(
                    new UseCompatibleCmdletsConfiguration { Common = new CommonConfiguration(enable: true), Compatibility = new[] { "core-6.1.0-windows" } }))
                .Build();
        }

        [Fact]
        public void CompatibleCmdlet_ShouldNotReturnViolation()
        {
            var script = @"Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
