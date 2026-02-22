using System.Collections.Generic;
using System.Linq;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules;
using PSpecter.Execution;
using Xunit;

namespace PSpecter.Test.Rules
{
    public class AvoidUsingConvertToSecureStringWithPlainTextTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingConvertToSecureStringWithPlainTextTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingConvertToSecureStringWithPlainText>())
                .Build();
        }

        [Fact]
        public void ConvertToSecureStringWithAsPlainText_ShouldReturnViolation()
        {
            var script = @"ConvertTo-SecureString 'password' -AsPlainText -Force";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingConvertToSecureStringWithPlainText", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Error, violation.Severity);
        }

        [Fact]
        public void ConvertToSecureStringWithoutAsPlainText_ShouldNotReturnViolation()
        {
            var script = @"ConvertTo-SecureString $encryptedString";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void NoConvertToSecureString_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Select-Object Name";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void AsPlainTextCaseInsensitive_ShouldReturnViolation()
        {
            var script = @"ConvertTo-SecureString 'password' -asplaintext -Force";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void CtssAlias_WithAsPlainText_ShouldReturnViolation()
        {
            var script = @"ctss 'sneaky convert' -AsPlainText -Force";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }
    }
}
