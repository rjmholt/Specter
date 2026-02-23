using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidVariableShadowingTests
    {
        private ScriptAnalyzer BuildAnalyzer(string[]? excludeVariables = null)
        {
            var config = new AvoidVariableShadowingConfiguration();
            if (excludeVariables is not null)
            {
                config.ExcludeVariables = excludeVariables;
            }

            return new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<AvoidVariableShadowing, AvoidVariableShadowingConfiguration>(config))
                .Build();
        }

        [Fact]
        public void InnerScopeShadowsOuter_ShouldReturnViolation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    $x = 1
    function Inner {
        $x = 2
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Contains(violations, v => v.Rule!.Name == "AvoidVariableShadowing" && v.Message.Contains("x"));
        }

        [Fact]
        public void ShadowingParameter_ShouldReturnViolation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    param($name)
    function Inner {
        $name = 'shadowed'
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Contains(violations, v => v.Message.Contains("name"));
        }

        [Fact]
        public void NoShadowing_ShouldNotReturnViolation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    $x = 1
    function Inner {
        $y = 2
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidVariableShadowing").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ExcludedVariable_ShouldNotReturnViolation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    $_ = 'outer'
    1..10 | ForEach-Object { $_ }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidVariableShadowing").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ScopeQualifiedVariable_ShouldNotReturnViolation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    $x = 1
    function Inner {
        $script:x = 2
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidVariableShadowing").ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void Severity_ShouldBeInformation()
        {
            var analyzer = BuildAnalyzer();
            var script = @"
function Outer {
    $x = 1
    function Inner {
        $x = 2
    }
}";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script)
                .Where(v => v.Rule!.Name == "AvoidVariableShadowing").ToList();

            Assert.All(violations, v => Assert.Equal(DiagnosticSeverity.Information, v.Severity));
        }
    }
}
