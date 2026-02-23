using System.Collections.Generic;
using System.Linq;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using Specter;
using Specter.Builder;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseDeclaredVarsMoreThanAssignmentsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseDeclaredVarsMoreThanAssignmentsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseDeclaredVarsMoreThanAssignments>())
                .Build();
        }

        [Fact]
        public void VariableAssignedButNeverUsed_ShouldReturnViolation()
        {
            var script = "$x = 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseDeclaredVarsMoreThanAssignments", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("x", violation.Message);
        }

        [Fact]
        public void VariableAssignedAndUsed_ShouldNotReturnViolation()
        {
            var script = "$x = 1; $x";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void VariableUsedInExpression_ShouldNotReturnViolation()
        {
            var script = "$x = 1; $x + 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
