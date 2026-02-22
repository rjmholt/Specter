using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidEmptyCatchBlockTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidEmptyCatchBlockTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidEmptyCatchBlock>())
                .Build();
        }

        [Fact]
        public void EmptyCatchBlock_ShouldReturnViolation()
        {
            var script = @"
try {
    1/0
}
catch {
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingEmptyCatchBlock", oneViolation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Warning, oneViolation.Severity);
            Assert.Equal(5, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(6, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(2, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void EmptyCatchBlockWithSpecificException_ShouldReturnViolation()
        {
            var script = @"
try {
    1/0
}
catch [DivideByZeroException] {
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void MultipleCatchBlocks_BothEmpty_ShouldReturnTwoViolations()
        {
            var script = @"
try {
    1/0
}
catch [DivideByZeroException] {
}
catch [System.Exception] {
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void CatchBlockWithWriteError_ShouldNotReturnViolation()
        {
            var script = @"
try {
    1/0
}
catch {
    Write-Error 'An error occurred'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void CatchBlockWithThrow_ShouldNotReturnViolation()
        {
            var script = @"
try {
    1/0
}
catch {
    throw 'Custom error'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void CatchBlockWithComment_ShouldReturnViolation()
        {
            var script = @"
try {
    1/0
}
catch {
    # This is just a comment, not actual code
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NestedTryCatch_EmptyInnerCatch_ShouldReturnViolation()
        {
            var script = @"
try {
    try {
        1/0
    }
    catch {
    }
}
catch {
    Write-Error 'Outer catch'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(6, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(5, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(7, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(6, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void MixedCatchBlocks_OneEmptyOneNot_ShouldReturnOneViolation()
        {
            var script = @"
try {
    1/0
}
catch [DivideByZeroException] {
}
catch [System.Exception] {
    Write-Host 'Exception handled'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NoTryCatchBlocks_ShouldReturnNoViolations()
        {
            var script = @"
Write-Host 'Hello World'
$x = 1 + 1";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}