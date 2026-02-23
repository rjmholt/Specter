using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUnsafeEscapingTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUnsafeEscapingTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUnsafeEscaping>())
                .Build();
        }

        [Fact]
        public void ReplaceWithDoubledSingleQuotes_ShouldReturnViolation()
        {
            var script = @"$s -replace ""''""";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUnsafeEscaping", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ReplaceWithDoubledDoubleQuotes_ShouldReturnViolation()
        {
            // '""' as the replacement operand
            var script = "$s -replace '\"\"'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ReplaceWithBacktickQuote_ShouldReturnViolation()
        {
            // Backtick-single-quote pattern in single-quoted string
            var script = @"$s -replace '`'''";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ReplaceWithSafePattern_ShouldNotReturnViolation()
        {
            var script = @"$s -replace 'foo', 'bar'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonReplaceOperator_ShouldNotReturnViolation()
        {
            var script = @"$s -match ""''""";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoReplace_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
