using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AlignAssignmentStatement", typeof(Strings), nameof(Strings.AlignAssignmentStatementDescription), Severity = DiagnosticSeverity.Warning)]
    public class AlignAssignmentStatement : FormattingRule<AlignAssignmentStatementEditorConfiguration>
    {
        public AlignAssignmentStatement(RuleInfo ruleInfo, AlignAssignmentStatementEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new AlignAssignmentStatementEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.AlignAssignmentStatementError;
    }
}
