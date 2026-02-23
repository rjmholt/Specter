using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidSmartQuotesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidSmartQuotesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidSmartQuotes>())
                .Build();
        }

        [Fact]
        public void CurlySingleQuote_ShouldReturnViolation()
        {
            // U+2018 left single curly quote outside a string
            var script = "Get-Process \u2018Name\u2019";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.True(violations.Count >= 1);
            Assert.All(violations, v => Assert.Equal("AvoidSmartQuotes", v.Rule!.Name));
            Assert.All(violations, v => Assert.Equal(DiagnosticSeverity.Warning, v.Severity));
        }

        [Fact]
        public void CurlyDoubleQuote_ShouldReturnViolation()
        {
            // U+201C / U+201D outside a string
            var script = "Get-Process \u201CName\u201D";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.True(violations.Count >= 1);
        }

        [Fact]
        public void EmDash_ShouldReturnViolation()
        {
            // U+2014 em dash outside a string
            var script = "$x \u2014 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.True(violations.Count >= 1);
        }

        [Fact]
        public void StraightQuotes_ShouldNotReturnViolation()
        {
            var script = @"Get-Process -Name 'foo'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoSpecialCharacters_ShouldNotReturnViolation()
        {
            var script = @"$x = 1 + 2";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
