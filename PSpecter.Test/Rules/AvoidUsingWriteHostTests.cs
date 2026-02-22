using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidUsingWriteHostTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingWriteHostTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingWriteHost>())
                .Build();
        }

        [Fact]
        public void WriteHost_ShouldReturnViolation()
        {
            var script = @"Write-Host 'Hello World'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWriteHost", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void MultipleWriteHost_ShouldReturnMultipleViolations()
        {
            var script = @"
Write-Host 'First'
Write-Host 'Second'
Write-Host 'Third'
Write-Host 'Fourth'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(4, violations.Count);
        }

        [Fact]
        public void WriteOutput_ShouldNotReturnViolation()
        {
            var script = @"Write-Output 'Hello World'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ShowFunction_WriteHostInside_ShouldNotReturnViolation()
        {
            var script = @"
function Show-Message {
    Write-Host 'Hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ConsoleWriteLine_ShouldReturnViolation()
        {
            var script = @"[Console]::WriteLine('Hello')";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void SystemConsoleWrite_ShouldReturnViolation()
        {
            var script = @"[System.Console]::Write('Hello')";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void WriteHostCaseInsensitive_ShouldReturnViolation()
        {
            var script = @"write-HOST 'Hello'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }
    }
}
