using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidEmptyNamedBlocksTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidEmptyNamedBlocksTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidEmptyNamedBlocks>())
                .Build();
        }

        [Fact]
        public void EmptyBeginBlock_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    begin { }
    end { 'hello' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidEmptyNamedBlocks", violation.Rule!.Name);
            Assert.Contains("begin", violation.Message);
        }

        [Fact]
        public void EmptyProcessBlock_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    process { }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("process", violation.Message);
        }

        [Fact]
        public void EmptyEndBlock_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    begin { 'setup' }
    end { }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("end", violation.Message);
        }

        [Fact]
        public void EmptyDynamicParamBlock_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    dynamicparam { }
    end { 'hello' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("dynamicparam", violation.Message);
        }

        [Fact]
        public void MultipleEmptyBlocks_ShouldReturnMultipleViolations()
        {
            var script = @"
function Foo {
    begin { }
    process { }
    end { 'hello' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void ImplicitEndBlock_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    'hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PopulatedNamedBlocks_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    begin { $x = 1 }
    process { Write-Output $_ }
    end { Write-Output 'done' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoFunction_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
