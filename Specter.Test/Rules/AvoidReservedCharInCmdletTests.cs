using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidReservedCharInCmdletTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidReservedCharInCmdletTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidReservedCharInCmdlet>())
                .Build();
        }

        [Fact]
        public void ExportedFunctionWithReservedChar_ShouldReturnViolation()
        {
            // ReservedCmdletChar only flags functions that are exported via Export-ModuleMember
            // and have [CmdletBinding()]. Reserved chars include #.
            var script = @"
function Test-#Func {
    [CmdletBinding()]
    param()
}
Export-ModuleMember -Function 'Test-#Func'
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("ReservedCmdletChar", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NormalFunction_ShouldNotReturnViolation()
        {
            var script = @"function Get-MyThing { [CmdletBinding()] param() }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScriptWithoutExportModuleMember_ShouldNotReturnViolation()
        {
            // When no Export-ModuleMember is found, the rule skips entirely.
            var script = @"
function Test-#Func {
    [CmdletBinding()]
    param()
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
