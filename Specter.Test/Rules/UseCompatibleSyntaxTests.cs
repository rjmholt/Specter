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
    public class UseCompatibleSyntaxTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseCompatibleSyntaxTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseCompatibleSyntax, UseCompatibleSyntaxConfiguration>(
                    new UseCompatibleSyntaxConfiguration { Common = new CommonConfiguration(enable: true), TargetVersions = new[] { "5.0" } }))
                .Build();
        }

        [Fact]
        public void BasicSyntax_ShouldNotReturnViolation()
        {
            var script = @"Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
