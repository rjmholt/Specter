using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Execution;
using Specter.Rules.Builtin.Rules;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseErrorActionPreferenceConsistentlyTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseErrorActionPreferenceConsistentlyTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseErrorActionPreferenceConsistently>())
                .Build();
        }

        [Fact]
        public void StopWithoutTryCatch_ShouldReturnSuggestion()
        {
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(@"
$ErrorActionPreference = 'Stop'
Get-Item missing.txt").ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseErrorActionPreferenceConsistently", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
        }

        [Fact]
        public void StopWithTryCatch_ShouldNotReturnSuggestion()
        {
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(@"
$ErrorActionPreference = 'Stop'
try {
    Get-Item missing.txt
} catch {
    Write-Verbose $_
}").ToList();

            Assert.Empty(violations);
        }
    }
}
