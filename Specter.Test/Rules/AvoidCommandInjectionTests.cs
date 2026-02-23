using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidCommandInjectionTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidCommandInjectionTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidCommandInjection>())
                .Build();
        }

        [Theory]
        [InlineData("cmd /c \"echo $var\"")]
        [InlineData("cmd.exe /c \"net user $name\"")]
        [InlineData("powershell -Command \"& $script\"")]
        [InlineData("pwsh -c \"Invoke-Item $path\"")]
        public void ShellWithExpandableString_ShouldReturnViolation(string script)
        {
            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidCommandInjection", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void CmdWithConstantString_ShouldNotReturnViolation()
        {
            var script = @"cmd /c 'echo hello'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void CmdWithVariableArgument_ShouldNotReturnViolation()
        {
            var script = @"cmd /c $plainVariable";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NonShellCommand_ShouldNotReturnViolation()
        {
            var script = "Get-Process -Name \"$processName\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void CmdCaseInsensitive_ShouldReturnViolation()
        {
            var script = "CMD /C \"echo $var\"";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }
    }
}
