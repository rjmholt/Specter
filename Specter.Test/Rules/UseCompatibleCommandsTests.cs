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
    public class UseCompatibleCommandsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseCompatibleCommandsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().UseBuiltinDatabase().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseCompatibleCommands, UseCompatibleCommandsConfiguration>(
                    new UseCompatibleCommandsConfiguration { Common = new CommonConfiguration(enable: true), TargetProfiles = new[] { "core-6.1.0-windows" } }))
                .Build();
        }

        [Fact]
        public void CompatibleCommand_ShouldNotReturnViolation()
        {
            var script = @"Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
