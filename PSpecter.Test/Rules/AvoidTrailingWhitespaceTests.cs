using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidTrailingWhitespaceTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidTrailingWhitespaceTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidTrailingWhitespace>())
                .Build();
        }

        [Fact]
        public void TrailingSpace_ShouldReturnViolation()
        {
            var script = "Get-Process ";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidTrailingWhitespace", violation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void TrailingTab_ShouldReturnViolation()
        {
            var script = "Get-Process\t";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void MultipleLines_OneWithTrailingSpace_ShouldReturnOneViolation()
        {
            var script = "Get-Process \nGet-Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void MultipleLines_BothWithTrailingSpace_ShouldReturnTwoViolations()
        {
            var script = "Get-Process \nGet-Service ";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void NoTrailingWhitespace_ShouldNotReturnViolation()
        {
            var script = "Get-Process";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void EmptyLine_ShouldNotReturnViolation()
        {
            var script = "Get-Process\n\nGet-Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
