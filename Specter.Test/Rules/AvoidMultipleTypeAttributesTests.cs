using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidMultipleTypeAttributesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidMultipleTypeAttributesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidMultipleTypeAttributes>())
                .Build();
        }

        [Fact]
        public void MultipleTypeAttributesOnParameter_ShouldReturnViolation()
        {
            var script = @"param([string][int]$x)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidMultipleTypeAttributes", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void SingleTypeAttribute_ShouldNotReturnViolation()
        {
            var script = @"param([string]$x)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
