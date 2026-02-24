using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Execution;
using Specter.Rules.Builtin.Rules;
using Xunit;

namespace Specter.Test.Rules
{
    public class SuggestCmdletBindingTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public SuggestCmdletBindingTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<SuggestCmdletBinding>())
                .Build();
        }

        [Fact]
        public void ExportedModuleFunctionWithoutCmdletBinding_ShouldReturnSuggestion()
        {
            var script = @"
function Do-Thing {
    param([string]$Name)
}
Export-ModuleMember -Function Do-Thing";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(
                System.Management.Automation.Language.Parser.ParseInput(script, out var tokens, out _),
                tokens,
                scriptPath: "MyModule.psm1").ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("SuggestCmdletBinding", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void FunctionWithCmdletBinding_ShouldNotReturnSuggestion()
        {
            var script = @"
function Do-Thing {
    [CmdletBinding()]
    param([string]$Name)
}
Export-ModuleMember -Function Do-Thing";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(
                System.Management.Automation.Language.Parser.ParseInput(script, out var tokens, out _),
                tokens,
                scriptPath: "MyModule.psm1").ToList();

            Assert.Empty(violations);
        }
    }
}
