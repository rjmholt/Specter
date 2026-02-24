using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseSingularNounsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseSingularNounsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseSingularNouns, UseSingularNounsConfiguration>(
                    new UseSingularNounsConfiguration()))
                .Build();
        }

        [Fact]
        public void FunctionWithPluralNoun_ShouldReturnViolation()
        {
            var script = "function Get-Items { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseSingularNouns", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void FunctionWithSingularNoun_ShouldNotReturnViolation()
        {
            var script = "function Get-Item { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
