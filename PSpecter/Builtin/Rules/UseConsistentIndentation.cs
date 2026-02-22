using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseConsistentIndentation", typeof(Strings), nameof(Strings.UseConsistentIndentationDescription), Severity = DiagnosticSeverity.Warning)]
    public class UseConsistentIndentation : FormattingRule<UseConsistentIndentationEditorConfiguration>
    {
        public UseConsistentIndentation(RuleInfo ruleInfo, UseConsistentIndentationEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new UseConsistentIndentationEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.UseConsistentIndentationError;
    }
}
