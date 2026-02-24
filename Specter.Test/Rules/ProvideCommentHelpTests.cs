using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class ProvideCommentHelpTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public ProvideCommentHelpTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<ProvideCommentHelp, ProvideCommentHelpConfiguration>(
                    new ProvideCommentHelpConfiguration { ExportedOnly = false }))
                .Build();
        }

        [Fact]
        public void FunctionWithoutCommentHelp_ShouldReturnViolation()
        {
            var script = @"
function Foo {
    'hello'
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("ProvideCommentHelp", violation.Rule!.Name);
            Assert.Contains("Foo", violation.Message);
        }

        [Fact]
        public void FunctionWithCommentHelp_ShouldNotReturnViolation()
        {
            var script = @"
<#
.SYNOPSIS
    Short description
.DESCRIPTION
    Long description
#>
function Foo {
    'hello'
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
