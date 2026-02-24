using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseUsingScopeModifierInNewRunspacesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseUsingScopeModifierInNewRunspacesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseUsingScopeModifierInNewRunspaces>())
                .Build();
        }

        [Fact]
        public void StartJobWithVariable_ShouldReturnViolation()
        {
            var script = "Start-Job { $x }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseUsingScopeModifierInNewRunspaces", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("$x", violation.Message);
        }

        [Fact]
        public void StartJobWithUsingVariable_ShouldNotReturnViolation()
        {
            var script = "Start-Job { $using:x }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NormalScriptWithVariable_ShouldNotReturnViolation()
        {
            var script = "$x = 1; $x";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
