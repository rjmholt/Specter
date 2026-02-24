using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingBrokenHashAlgorithmsTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingBrokenHashAlgorithmsTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingBrokenHashAlgorithms>())
                .Build();
        }

        [Fact]
        public void GetFileHashWithMD5_ShouldReturnViolation()
        {
            var script = "Get-FileHash -Algorithm MD5 -Path 'file.txt'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingBrokenHashAlgorithms", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void GetFileHashWithSHA1_ShouldReturnViolation()
        {
            var script = "Get-FileHash -Algorithm SHA1 -Path 'file.txt'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void GetFileHashWithSHA256_ShouldNotReturnViolation()
        {
            var script = "Get-FileHash -Algorithm SHA256 -Path 'file.txt'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
