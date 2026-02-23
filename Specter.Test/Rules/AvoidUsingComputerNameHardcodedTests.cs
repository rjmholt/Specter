using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingComputerNameHardcodedTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingComputerNameHardcodedTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingComputerNameHardcoded>())
                .Build();
        }

        [Fact]
        public void HardcodedComputerName_ShouldReturnViolation()
        {
            var script = "Invoke-Command -ComputerName 'SERVER01' -ScriptBlock { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingComputerNameHardcoded", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Error, violation.Severity);
        }

        [Fact]
        public void VariableComputerName_ShouldNotReturnViolation()
        {
            var script = "Invoke-Command -ComputerName $server -ScriptBlock { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void Localhost_ShouldNotReturnViolation()
        {
            var script = "Invoke-Command -ComputerName 'localhost' -ScriptBlock { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
