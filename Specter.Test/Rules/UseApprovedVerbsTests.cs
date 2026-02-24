using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseApprovedVerbsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseApprovedVerbsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseApprovedVerbs>())
                .Build();
        }

        [Fact]
        public void UnapprovedVerb_ShouldReturnViolation()
        {
            var script = @"function Do-Something {}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseApprovedVerbs", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("Do-Something", violation.Message);
        }

        [Fact]
        public void ApprovedVerb_ShouldNotReturnViolation()
        {
            var script = @"function Get-Something {}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
