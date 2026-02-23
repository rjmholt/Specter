using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class PossibleIncorrectUsageOfRedirectionOperatorTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PossibleIncorrectUsageOfRedirectionOperatorTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<PossibleIncorrectUsageOfRedirectionOperator>())
                .Build();
        }

        [Fact]
        public void FileRedirectionInIfCondition_ShouldReturnViolation()
        {
            var script = "if (Get-Process 2> errors.txt) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PossibleIncorrectUsageOfRedirectionOperator", violation.Rule!.Name);
        }

        [Fact]
        public void NormalIfCondition_ShouldNotReturnViolation()
        {
            var script = "if ($x -eq 1) { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void RedirectionOutsideIf_ShouldNotReturnViolation()
        {
            var script = "Get-Process 2>&1 | Out-Null";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
