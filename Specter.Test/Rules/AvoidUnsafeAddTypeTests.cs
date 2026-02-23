using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUnsafeAddTypeTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUnsafeAddTypeTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUnsafeAddType>())
                .Build();
        }

        [Fact]
        public void VariableTypeDefinition_ShouldReturnViolation()
        {
            var script = @"Add-Type -TypeDefinition $code";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUnsafeAddType", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void VariableMemberDefinition_ShouldReturnViolation()
        {
            var script = @"Add-Type -MemberDefinition $memberCode -Namespace 'Test' -Name 'Test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ExpandableStringTypeDefinition_ShouldReturnViolation()
        {
            var script = "Add-Type -TypeDefinition \"public class $name {}\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ConstantTypeDefinition_ShouldNotReturnViolation()
        {
            var script = @"Add-Type -TypeDefinition 'public class Foo { }'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PathParameter_ShouldNotReturnViolation()
        {
            var script = @"Add-Type -Path 'mylib.dll'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoAddType_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
