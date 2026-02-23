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
    public class AvoidGlobalAliasesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidGlobalAliasesTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidGlobalAliases>())
                .Build();
        }

        [Fact]
        public void NewAliasWithGlobalScopeInModule_ShouldReturnViolation()
        {
            var script = @"New-Alias -Scope Global -Name x -Value y";
            var ast = Parser.ParseInput(script, out Token[] tokens, out _);

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(ast, tokens, "test.psm1").ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidGlobalAliases", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NewAliasWithoutGlobalScope_ShouldNotReturnViolation()
        {
            var script = @"New-Alias -Scope Script -Name x -Value y";
            var ast = Parser.ParseInput(script, out Token[] tokens, out _);

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScript(ast, tokens, "test.psm1").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonModuleScript_ShouldNotReturnViolation()
        {
            // AvoidGlobalAliases only fires when script path ends in .psm1.
            // AnalyzeScriptInput passes null path, so the rule yields nothing.
            var script = @"New-Alias -Scope Global -Name x -Value y";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
