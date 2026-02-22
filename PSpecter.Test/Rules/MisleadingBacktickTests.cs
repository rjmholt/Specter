using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class MisleadingBacktickTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public MisleadingBacktickTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<MisleadingBacktick>())
                .Build();
        }

        [Fact]
        public void BacktickFollowedBySpace_ShouldReturnViolation()
        {
            var script = "Get-Process ` \nGet-Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("MisleadingBacktick", violation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void BacktickFollowedByTab_ShouldReturnViolation()
        {
            var script = "Get-Process `\t\nGet-Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void BacktickForLineContinuation_ShouldNotReturnViolation()
        {
            var script = "Get-Process `\n-Name 'foo'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoBacktick_ShouldNotReturnViolation()
        {
            var script = "Get-Process\nGet-Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void BacktickInMiddleOfLine_ShouldNotReturnViolation()
        {
            var script = "Write-Host \"Hello`nWorld\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
