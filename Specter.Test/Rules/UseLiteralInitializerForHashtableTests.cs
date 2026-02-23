using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseLiteralInitializerForHashtableTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseLiteralInitializerForHashtableTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseLiteralInitializerForHashtable>())
                .Build();
        }

        [Fact]
        public void HashtableNew_ShouldReturnViolation()
        {
            var script = "[hashtable]::new()";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseLiteralInitializerForHashtable", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void NewObjectHashtable_ShouldReturnViolation()
        {
            var script = "New-Object hashtable";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("UseLiteralInitializerForHashtable", violation.Rule!.Name);
        }

        [Fact]
        public void LiteralHashtable_ShouldNotReturnViolation()
        {
            var script = "@{}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void LiteralHashtableWithContent_ShouldNotReturnViolation()
        {
            var script = "@{ Key = 'Value' }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
