using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class PossibleIncorrectUsageOfAssignmentOperatorTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PossibleIncorrectUsageOfAssignmentOperatorTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<PossibleIncorrectUsageOfAssignmentOperator>())
                .Build();
        }

        [Fact]
        public void AssignmentInIfCondition_ShouldReturnViolation()
        {
            var script = "if ($x = 1) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PossibleIncorrectUsageOfAssignmentOperator", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void ComparisonInIfCondition_ShouldNotReturnViolation()
        {
            var script = "if ($x -eq 1) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void AssignmentInParentheses_ShouldNotReturnViolation()
        {
            var script = "if (($x = Get-Process)) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
