using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class ReviewUnusedParameterTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public ReviewUnusedParameterTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<ReviewUnusedParameter, ReviewUnusedParameterConfiguration>(
                    new ReviewUnusedParameterConfiguration()))
                .Build();
        }

        [Fact]
        public void UnusedParameter_ShouldReturnViolation()
        {
            var script = @"function Foo { param($x) 'hello' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("ReviewUnusedParameter", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("x", violation.Message);
        }

        [Fact]
        public void UsedParameter_ShouldNotReturnViolation()
        {
            var script = @"function Foo { param($x) $x }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
