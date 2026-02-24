using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidNestedFunctionsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidNestedFunctionsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidNestedFunctions>())
                .Build();
        }

        [Fact]
        public void NestedFunction_ShouldReturnViolation()
        {
            var script = @"
function Outer {
    function Inner {
        'hello'
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidNestedFunctions", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("Inner", violation.Message);
            Assert.Contains("Outer", violation.Message);
        }

        [Fact]
        public void DeeplyNestedFunction_ShouldReturnViolations()
        {
            var script = @"
function A {
    function B {
        function C {
            'deep'
        }
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            // B nested in A, C nested in B
            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void TopLevelFunction_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    'hello'
}

function Bar {
    'world'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoFunctions_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
