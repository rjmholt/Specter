using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidOverwritingBuiltInCmdletsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidOverwritingBuiltInCmdletsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidOverwritingBuiltInCmdlets, AvoidOverwritingBuiltInCmdletsConfiguration>(
                    new AvoidOverwritingBuiltInCmdletsConfiguration()))
                .Build();
        }

        [Fact]
        public void FunctionOverwritingBuiltInCmdlet_ShouldReturnViolation()
        {
            var script = @"function Get-ChildItem { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidOverwritingBuiltInCmdlets", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NovelFunctionName_ShouldNotReturnViolation()
        {
            var script = @"function Get-MyCustomThing { }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
