using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidDangerousMethodTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidDangerousMethodTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidDangerousMethod>())
                .Build();
        }

        [Theory]
        [InlineData("$execCtx.InvokeScript($code)")]
        [InlineData("$pipeline.AddScript($code)")]
        [InlineData("$host.NewScriptBlock($code)")]
        [InlineData("$execCtx.ExpandString($code)")]
        [InlineData("$runspace.CreateNestedPipeline($cmd, $false)")]
        public void DangerousMethod_ShouldReturnViolation(string script)
        {
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidDangerousMethod", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void ScriptBlockCreate_ShouldReturnViolation()
        {
            var script = @"[scriptblock]::Create($code)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidDangerousMethod", violation.Rule!.Name);
        }

        [Fact]
        public void ScriptBlockCreateFullTypeName_ShouldReturnViolation()
        {
            var script = @"[System.Management.Automation.ScriptBlock]::Create($code)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NonDangerousStaticCreate_ShouldNotReturnViolation()
        {
            var script = @"[System.IO.File]::Create('test.txt')";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SafeMethodCall_ShouldNotReturnViolation()
        {
            var script = @"$obj.ToString()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void MultipleDangerousCalls_ShouldReturnMultipleViolations()
        {
            var script = @"
$execCtx.InvokeScript($code)
$pipeline.AddScript($more)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }
    }
}
