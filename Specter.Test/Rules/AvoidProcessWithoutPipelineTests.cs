using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidProcessWithoutPipelineTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidProcessWithoutPipelineTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidProcessWithoutPipeline>())
                .Build();
        }

        [Fact]
        public void ProcessBlockWithoutPipelineParam_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    param($Name)
    process {
        Write-Output $Name
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidProcessWithoutPipeline", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("Foo", violation.Message);
        }

        [Fact]
        public void ProcessBlockWithValueFromPipeline_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    param(
        [Parameter(ValueFromPipeline)]
        $InputObject
    )
    process {
        Write-Output $InputObject
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ProcessBlockWithValueFromPipelineByPropertyName_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    param(
        [Parameter(ValueFromPipelineByPropertyName)]
        $Name
    )
    process {
        Write-Output $Name
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoProcessBlock_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    param($Name)
    Write-Output $Name
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ProcessBlockWithValueFromPipelineTrue_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    param(
        [Parameter(ValueFromPipeline = $true)]
        $InputObject
    )
    process {
        Write-Output $InputObject
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
