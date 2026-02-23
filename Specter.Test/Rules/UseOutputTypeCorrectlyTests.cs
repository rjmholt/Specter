using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseOutputTypeCorrectlyTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseOutputTypeCorrectlyTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseOutputTypeCorrectly>())
                .Build();
        }

        [Fact]
        public void FunctionWithMatchingOutputType_ShouldNotReturnViolation()
        {
            var script = @"
function Get-Test {
    [CmdletBinding()]
    [OutputType([string])]
    param()
    return 'hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void FunctionWithoutOutputType_ShouldNotReturnViolation()
        {
            var script = @"
function Get-Test {
    [CmdletBinding()]
    param()
    Write-Output 'hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
