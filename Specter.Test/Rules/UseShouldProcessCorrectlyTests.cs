using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseShouldProcessCorrectlyTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseShouldProcessCorrectlyTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseShouldProcessCorrectly>())
                .Build();
        }

        [Fact]
        public void ShouldProcessWithoutSupportsShouldProcess_ShouldReturnViolation()
        {
            var script = @"
function Test-Foo {
    param()
    if ($PSCmdlet.ShouldProcess('target')) {
        Write-Output 'done'
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("ShouldProcess", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ShouldProcessWithSupportsShouldProcess_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Foo {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if ($PSCmdlet.ShouldProcess('target')) {
        Write-Output 'done'
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
