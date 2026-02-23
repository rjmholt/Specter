using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UsePSCredentialTypeTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UsePSCredentialTypeTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UsePSCredentialType>())
                .Build();
        }

        [Fact]
        public void CredentialParamWithStringType_ShouldReturnViolation()
        {
            var script = @"
function Test-Foo {
    param([string]$Credential)
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UsePSCredentialType", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void CredentialParamWithPSCredentialType_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Foo {
    param([PSCredential]$Credential)
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
