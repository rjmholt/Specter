using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidFilterKeywordTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidFilterKeywordTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AvoidFilterKeyword", null },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AvoidFilterKeyword>())
                .Build();
        }

        [Fact]
        public void FilterKeyword_ShouldReturnViolation()
        {
            var script = @"
filter MyFilter {
    $_
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidFilterKeyword", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("MyFilter", violation.Message);
        }

        [Fact]
        public void FunctionWithProcessBlock_ShouldNotReturnViolation()
        {
            var script = @"
function MyFunc {
    process {
        $_
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void RegularFunction_ShouldNotReturnViolation()
        {
            var script = @"
function Foo {
    'hello'
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void DisabledByDefault()
        {
            var defaultAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidFilterKeyword>())
                .Build();

            var script = @"
filter MyFilter {
    $_
}";

            IReadOnlyList<ScriptDiagnostic> violations = defaultAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
