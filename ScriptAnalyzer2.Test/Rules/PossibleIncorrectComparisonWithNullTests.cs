using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Xunit;

namespace ScriptAnalyzer2.Test.Rules
{
    public class PossibleIncorrectComparisonWithNullTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PossibleIncorrectComparisonWithNullTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<PossibleIncorrectComparisonWithNull>())
                .Build();
        }

        [Fact]
        public void NullOnRight_Eq_ShouldReturnViolation()
        {
            var script = @"if ($x -eq $null) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PossibleIncorrectComparisonWithNull", violation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NullOnRight_Ne_ShouldReturnViolation()
        {
            var script = @"if ($x -ne $null) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NullOnRight_Ceq_ShouldReturnViolation()
        {
            var script = @"if ($x -ceq $null) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NullOnLeft_ShouldNotReturnViolation()
        {
            var script = @"if ($null -eq $x) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoNullComparison_ShouldNotReturnViolation()
        {
            var script = @"if ($x -eq $y) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NullOnRight_HasSuggestedCorrection()
        {
            var script = @"if ($x -eq $null) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.NotNull(violation.Corrections);
            Assert.Single(violation.Corrections);
            Assert.Contains("$null -eq $x", violation.Corrections[0].CorrectionText);
        }

        [Fact]
        public void NonEqualityOperator_ShouldNotReturnViolation()
        {
            var script = @"if ($x -gt $null) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
