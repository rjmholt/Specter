using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseShouldProcessForStateChangingFunctionsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseShouldProcessForStateChangingFunctionsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseShouldProcessForStateChangingFunctions>())
                .Build();
        }

        [Fact]
        public void SetVerbWithoutShouldProcess_ShouldReturnViolation()
        {
            var script = @"
function Set-Foo {
    param([string]$Name)
    Write-Output $Name
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseShouldProcessForStateChangingFunctions", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void RemoveVerbWithoutShouldProcess_ShouldReturnViolation()
        {
            var script = @"
function Remove-Foo {
    param([string]$Name)
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void SetVerbWithShouldProcess_ShouldNotReturnViolation()
        {
            var script = @"
function Set-Foo {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Name)
    Write-Output $Name
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void GetVerb_ShouldNotReturnViolation()
        {
            var script = @"
function Get-Foo {
    param([string]$Name)
    Write-Output $Name
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
