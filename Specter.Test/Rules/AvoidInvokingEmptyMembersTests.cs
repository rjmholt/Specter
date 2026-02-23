using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidInvokingEmptyMembersTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidInvokingEmptyMembersTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidInvokingEmptyMembers>())
                .Build();
        }

        [Fact]
        public void DynamicMemberWithExpression_ShouldReturnViolation()
        {
            var script = @"$x.(1+1)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidInvokingEmptyMembers", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void RegularMethodInvocation_ShouldNotReturnViolation()
        {
            var script = @"$x.Method()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
