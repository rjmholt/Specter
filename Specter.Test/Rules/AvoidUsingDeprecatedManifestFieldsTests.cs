using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingDeprecatedManifestFieldsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingDeprecatedManifestFieldsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingDeprecatedManifestFields>())
                .Build();
        }

        [Fact]
        public void BasicScript_ShouldNotReturnViolation()
        {
            var script = "Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void FunctionDefinition_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Something {
    param([string]$Name)
    Get-Process -Name $Name
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
