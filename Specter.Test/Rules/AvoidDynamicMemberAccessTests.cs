using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidDynamicMemberAccessTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidDynamicMemberAccessTests()
        {
            // AvoidDynamicMemberAccess is disabled by default, so provide config with it enabled
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidDynamicMemberAccess", null },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidDynamicMemberAccess>())
                .Build();
        }

        [Fact]
        public void DynamicPropertyAccess_ShouldReturnViolation()
        {
            var script = @"$obj.$prop";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidDynamicMemberAccess", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("property", violation.Message);
        }

        [Fact]
        public void DynamicMethodCall_ShouldReturnViolation()
        {
            var script = @"$obj.$method()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("method", violation.Message);
        }

        [Fact]
        public void ConstantPropertyAccess_ShouldNotReturnViolation()
        {
            var script = @"$obj.PropertyName";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ConstantMethodCall_ShouldNotReturnViolation()
        {
            var script = @"$obj.MethodName()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void StaticMethodCall_ShouldNotReturnViolation()
        {
            var script = @"[System.IO.Path]::GetTempPath()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void MultipleDynamicAccesses_ShouldReturnMultipleViolations()
        {
            var script = @"
$obj.$prop1
$obj.$prop2";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }
    }
}
