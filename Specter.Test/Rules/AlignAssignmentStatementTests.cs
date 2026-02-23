using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Editors;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AlignAssignmentStatementTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AlignAssignmentStatementTests()
        {
            var config = new Dictionary<string, IRuleConfiguration?>
            {
                { "PS/AlignAssignmentStatement", new AlignAssignmentStatementEditorConfiguration() },
            };
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(config!, ruleProvider => ruleProvider.AddRule<AlignAssignmentStatement>())
                .Build();
        }

        [Fact]
        public void MisalignedHashtableAssignments_ShouldReturnViolation()
        {
            var script = @"@{
    Name = 'Test'
    Value = 42
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AlignAssignmentStatement", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void AlignedHashtableAssignments_ShouldNotReturnViolation()
        {
            var script = @"@{
    Name  = 'Test'
    Value = 42
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void SingleLineHashtable_ShouldNotReturnViolation()
        {
            var script = @"@{ Name = 'Test'; Value = 42 }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
