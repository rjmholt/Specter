using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidNullOrEmptyHelpMessageAttributeTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidNullOrEmptyHelpMessageAttributeTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidNullOrEmptyHelpMessageAttribute>())
                .Build();
        }

        [Fact]
        public void EmptyHelpMessage_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(HelpMessage='')]
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidNullOrEmptyHelpMessageAttribute", violation.Rule!.Name);
        }

        [Fact]
        public void NullHelpMessage_ShouldReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(HelpMessage=$null)]
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ValidHelpMessage_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(HelpMessage='Enter a name')]
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoHelpMessage_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoParameterAttribute_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Function {
    param(
        [string]$Name
    )
}";
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
