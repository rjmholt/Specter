using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingDoubleQuotesForConstantStringTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingDoubleQuotesForConstantStringTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidUsingDoubleQuotesForConstantString", null },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidUsingDoubleQuotesForConstantString>())
                .Build();
        }

        [Fact]
        public void ConstantDoubleQuoted_ShouldReturnViolation()
        {
            var script = "\"constant\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingDoubleQuotesForConstantString", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void StringWithVariable_ShouldNotReturnViolation()
        {
            var script = "\"$variable\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SingleQuotedConstant_ShouldNotReturnViolation()
        {
            var script = "'constant'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
