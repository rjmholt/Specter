using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidNewObjectForPSCustomObjectTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidNewObjectForPSCustomObjectTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidNewObjectForPSCustomObject>())
                .Build();
        }

        [Fact]
        public void NewObjectPSObjectWithProperty_ShouldReturnViolation()
        {
            var script = @"New-Object -TypeName PSObject -Property @{ Name = 'Test' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidNewObjectForPSCustomObject", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void NewObjectPSCustomObjectWithProperty_ShouldReturnViolation()
        {
            var script = @"New-Object PSCustomObject -Property @{ Name = 'Test' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NewObjectPSObjectWithoutProperty_ShouldNotReturnViolation()
        {
            var script = @"New-Object PSObject";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NewObjectOtherType_ShouldNotReturnViolation()
        {
            var script = @"New-Object -TypeName System.Collections.ArrayList -Property @{ Capacity = 10 }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PSCustomObjectSyntax_ShouldNotReturnViolation()
        {
            var script = @"[PSCustomObject]@{ Name = 'Test' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
