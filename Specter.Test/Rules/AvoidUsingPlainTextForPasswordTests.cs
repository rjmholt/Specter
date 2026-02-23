using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingPlainTextForPasswordTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingPlainTextForPasswordTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingPlainTextForPassword>())
                .Build();
        }

        [Fact]
        public void StringPasswordParam_ShouldReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([string]$Password)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingPlainTextForPassword", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void SecureStringPasswordParam_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([SecureString]$Password)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PSCredentialParam_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([PSCredential]$Credential)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
