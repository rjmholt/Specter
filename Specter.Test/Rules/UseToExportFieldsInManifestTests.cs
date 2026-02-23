using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseToExportFieldsInManifestTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseToExportFieldsInManifestTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseToExportFieldsInManifest>())
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
        public void ScriptWithFunction_ShouldNotReturnViolation()
        {
            var script = @"
function Get-Foo { }
Get-Foo";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
