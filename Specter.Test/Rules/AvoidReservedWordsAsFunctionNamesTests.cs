using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidReservedWordsAsFunctionNamesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidReservedWordsAsFunctionNamesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidReservedWordsAsFunctionNames>())
                .Build();
        }

        [Fact]
        public void FunctionNamedBreak_ShouldReturnViolation()
        {
            var script = "function break {}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidReservedWordsAsFunctionNames", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void FunctionNamedCatch_ShouldReturnViolation()
        {
            var script = "function catch { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void FunctionWithNormalName_ShouldNotReturnViolation()
        {
            var script = "function Get-Process { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void FunctionWithNormalName_ShouldNotReturnViolation2()
        {
            var script = "function Test-Something { param($param1) }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
