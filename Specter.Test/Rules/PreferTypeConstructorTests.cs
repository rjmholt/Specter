using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class PreferTypeConstructorTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public PreferTypeConstructorTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/PreferTypeConstructor", null },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<PreferTypeConstructor>())
                .Build();
        }

        [Fact]
        public void NewObjectWithTypeName_ShouldReturnViolation()
        {
            var script = @"New-Object -TypeName System.Collections.ArrayList";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("PreferTypeConstructor", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("System.Collections.ArrayList", violation.Message);
        }

        [Fact]
        public void NewObjectPositionalTypeName_ShouldReturnViolation()
        {
            var script = @"New-Object System.Text.StringBuilder";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void NewObjectComObject_ShouldNotReturnViolation()
        {
            var script = @"New-Object -ComObject Shell.Application";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void TypeConstructorSyntax_ShouldNotReturnViolation()
        {
            var script = @"[System.Collections.ArrayList]::new()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void DisabledByDefault()
        {
            var defaultAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<PreferTypeConstructor>())
                .Build();

            var script = @"New-Object -TypeName System.Collections.ArrayList";

            IReadOnlyList<ScriptDiagnostic> violations = defaultAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
