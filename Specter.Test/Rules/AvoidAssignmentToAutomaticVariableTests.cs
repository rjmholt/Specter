using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidAssignmentToAutomaticVariableTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidAssignmentToAutomaticVariableTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidAssignmentToAutomaticVariable>())
                .Build();
        }

        [Fact]
        public void AssignmentToReadOnlyAutomaticVariable_ShouldReturnError()
        {
            var script = @"$true = 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidAssignmentToAutomaticVariable", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Error, violation.Severity);
        }

        [Fact]
        public void AssignmentToWritableAutomaticVariable_ShouldReturnWarning()
        {
            var script = @"$_ = 'x'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidAssignmentToAutomaticVariable", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void AssignmentToRegularVariable_ShouldNotReturnViolation()
        {
            var script = @"$myVar = 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
