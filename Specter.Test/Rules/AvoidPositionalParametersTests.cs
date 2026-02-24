using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidPositionalParametersTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidPositionalParametersTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidPositionalParameters, AvoidPositionalParametersConfiguration>(
                    new AvoidPositionalParametersConfiguration()))
                .Build();
        }

        [Fact]
        public void ManyPositionalParameters_ShouldReturnViolation()
        {
            // Rule flags when > 2 positional args; use a declared function with 3 positional args
            var script = @"
function Test-Func { [CmdletBinding()] param($a, $b, $c) }
Test-Func one two three";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingPositionalParameters", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void NamedParameters_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Func { [CmdletBinding()] param($a, $b, $c) }
Test-Func -a one -b two -c three";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidUsingPositionalParameters").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void FewPositionalParameters_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Func { [CmdletBinding()] param($a) }
Test-Func one";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidUsingPositionalParameters").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void VariadicFunction_ShouldNotReturnViolation()
        {
            var script = @"
function Invoke-Example { [CmdletBinding()] param([Parameter(ValueFromRemainingArguments)] [string[]] $Arguments) }
Invoke-Example one two three four";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidUsingPositionalParameters").ToList();

            Assert.Empty(violations);
        }
    }
}
