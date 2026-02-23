using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseSupportsShouldProcessTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseSupportsShouldProcessTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseSupportsShouldProcess>())
                .Build();
        }

        [Fact]
        public void FunctionWithWhatIfParam_ShouldReturnViolation()
        {
            var script = @"
function Test-Foo {
    param(
        [switch]$WhatIf
    )
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseSupportsShouldProcess", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void FunctionWithSupportsShouldProcess_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Foo {
    [CmdletBinding(SupportsShouldProcess)]
    param()
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
