using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUnreachableCodeTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUnreachableCodeTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUnreachableCode>())
                .Build();
        }

        [Fact]
        public void CodeAfterReturn_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    return 1
    Write-Host 'unreachable'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUnreachableCode", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Equal(4, violation.ScriptExtent.StartLineNumber);
        }

        [Fact]
        public void CodeAfterThrow_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    throw 'error'
    $x = 1
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void CodeAfterExit_ShouldReturnViolation()
        {
            var script = @"
exit 0
Write-Host 'unreachable'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void CodeAfterBreak_ShouldReturnViolation()
        {
            var script = @"
foreach ($i in 1..10) {
    break
    Write-Host $i
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void CodeAfterContinue_ShouldReturnViolation()
        {
            var script = @"
foreach ($i in 1..10) {
    continue
    Write-Host $i
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ConditionalReturnFollowedByCode_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    if ($x) { return }
    Write-Host 'reachable'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ReturnAsLastStatement_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $x = 1
    return $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoTerminator_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    $x = 1
    Write-Host $x
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void OnlyFlagsFirstUnreachableStatement()
        {
            var script = @"
function Foo {
    return 1
    Write-Host 'a'
    Write-Host 'b'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
            Assert.Equal(4, violations[0].ScriptExtent.StartLineNumber);
        }
    }
}
