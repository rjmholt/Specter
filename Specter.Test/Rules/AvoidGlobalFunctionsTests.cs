using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidGlobalFunctionsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidGlobalFunctionsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidGlobalFunctions>())
                .Build();
        }

        [Fact]
        public void FunctionWithGlobalPrefixInModule_ShouldReturnViolation()
        {
            var script = @"function Global:MyFunc { }";
            var ast = Parser.ParseInput(script, out Token[] tokens, out _);

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(ast, tokens, "test.psm1").ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidGlobalFunctions", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void RegularFunction_ShouldNotReturnViolation()
        {
            var script = @"function MyFunc { }";
            var ast = Parser.ParseInput(script, out Token[] tokens, out _);

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(ast, tokens, "test.psm1").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonModuleScript_ShouldNotReturnViolation()
        {
            // AvoidGlobalFunctions only fires when script path ends in .psm1.
            var script = @"function Global:MyFunc { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
