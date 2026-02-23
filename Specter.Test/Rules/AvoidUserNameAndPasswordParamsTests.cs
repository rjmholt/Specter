using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUserNameAndPasswordParamsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUserNameAndPasswordParamsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUserNameAndPasswordParams>())
                .Build();
        }

        [Fact]
        public void BothUserNameAndPassword_ShouldReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([string]$UserName, [string]$Password)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingUsernameAndPasswordParams", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Error, violation.Severity);
        }

        [Fact]
        public void SingleUserName_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([string]$UserName)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SinglePassword_ShouldNotReturnViolation()
        {
            var script = @"
function Test-Connect {
    param([string]$Password)
}
";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void Neither_ShouldNotReturnViolation()
        {
            var script = "function Get-Data { param([string]$Name) }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
