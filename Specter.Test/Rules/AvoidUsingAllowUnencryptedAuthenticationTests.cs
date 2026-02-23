using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingAllowUnencryptedAuthenticationTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingAllowUnencryptedAuthenticationTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingAllowUnencryptedAuthentication>())
                .Build();
        }

        [Fact]
        public void AllowUnencryptedAuthentication_ShouldReturnViolation()
        {
            var script = "Invoke-RestMethod -AllowUnencryptedAuthentication";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingAllowUnencryptedAuthentication", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void WithoutAllowUnencryptedAuthentication_ShouldNotReturnViolation()
        {
            var script = "Invoke-RestMethod -Uri 'https://example.com'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
