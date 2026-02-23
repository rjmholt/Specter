using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AlignAssignmentStatement", typeof(Strings), nameof(Strings.AlignAssignmentStatementDescription), Severity = DiagnosticSeverity.Warning)]
    internal class AlignAssignmentStatement : FormattingRule<AlignAssignmentStatementEditorConfiguration>
    {
        internal AlignAssignmentStatement(RuleInfo ruleInfo, AlignAssignmentStatementEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new AlignAssignmentStatementEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.AlignAssignmentStatementError;
    }
}
