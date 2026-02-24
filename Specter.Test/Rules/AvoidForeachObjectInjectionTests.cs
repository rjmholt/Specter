using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidForeachObjectInjectionTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidForeachObjectInjectionTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidForeachObjectInjection>())
                .Build();
        }

        [Fact]
        public void VariableMemberName_ShouldReturnViolation()
        {
            var script = @"Get-Process | ForEach-Object -MemberName $prop";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidForeachObjectInjection", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ConstantMemberName_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | ForEach-Object -MemberName 'Name'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScriptBlockProcess_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | ForEach-Object { $_.Name }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoForEachObject_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
