using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class MeasureFunctionComplexityTests
    {
        private ScriptAnalyzer BuildAnalyzer(int maxComplexity)
        {
            return new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<MeasureFunctionComplexity, MeasureFunctionComplexityConfiguration>(
                        new MeasureFunctionComplexityConfiguration { MaxComplexity = maxComplexity }))
                .Build();
        }

        [Fact]
        public void SimpleFunction_ShouldNotReturnViolation()
        {
            var analyzer = BuildAnalyzer(maxComplexity: 5);
            var script = @"
function Foo {
    Write-Host 'hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ComplexFunction_ShouldReturnViolation()
        {
            var analyzer = BuildAnalyzer(maxComplexity: 3);
            // Complexity: 1 (base) + 1 (if) + 1 (elseif) + 1 (foreach) + 1 (while) = 5
            var script = @"
function Complex {
    if ($a) { 'a' }
    elseif ($b) { 'b' }
    foreach ($i in 1..10) { $i }
    while ($c) { $c-- }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("MeasureFunctionComplexity", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
            Assert.Contains("Complex", violation.Message);
        }

        [Fact]
        public void FunctionExactlyAtThreshold_ShouldNotReturnViolation()
        {
            // Complexity: 1 (base) + 1 (if) = 2
            var analyzer = BuildAnalyzer(maxComplexity: 2);
            var script = @"
function TwoPath {
    if ($x) { 'a' } else { 'b' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void LogicalOperators_ContributeToComplexity()
        {
            // Complexity: 1 (base) + 1 (if) + 1 (-and) + 1 (-or) = 4
            var analyzer = BuildAnalyzer(maxComplexity: 2);
            var script = @"
function LogicHeavy {
    if ($a -and $b -or $c) { 'yes' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void SwitchCases_ContributeToComplexity()
        {
            // Complexity: 1 (base) + 3 (switch cases) = 4
            var analyzer = BuildAnalyzer(maxComplexity: 2);
            var script = @"
function SwitchHeavy {
    switch ($x) {
        'a' { 1 }
        'b' { 2 }
        'c' { 3 }
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void CatchAndTrap_ContributeToComplexity()
        {
            // Complexity: 1 (base) + 2 (catch clauses) + 1 (trap) = 4
            var analyzer = BuildAnalyzer(maxComplexity: 2);
            var script = @"
function ErrorHeavy {
    trap { 'trapped' }
    try { 1/0 }
    catch [DivideByZeroException] { 'div' }
    catch { 'other' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NestedFunctionComplexity_ShouldNotCountTowardOuter()
        {
            // Outer: 1 (base) = 1
            // Inner: 1 (base) + 3 (if, elseif, foreach) = 4 (flagged at threshold 3)
            var analyzer = BuildAnalyzer(maxComplexity: 3);
            var script = @"
function Outer {
    function Inner {
        if ($a) { 'a' }
        elseif ($b) { 'b' }
        foreach ($i in 1..10) { $i }
    }
    Inner
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("Inner", violation.Message);
        }

        [Fact]
        public void DefaultThreshold_25_AllowsModerateComplexity()
        {
            var analyzer = BuildAnalyzer(maxComplexity: 25);
            // Complexity: 1 (base) + 3 (ifs) = 4, well under 25
            var script = @"
function Moderate {
    if ($a) { 'a' }
    if ($b) { 'b' }
    if ($c) { 'c' }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
