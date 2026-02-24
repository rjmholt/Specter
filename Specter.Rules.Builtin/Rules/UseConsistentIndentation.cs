using Specter.Rules.Builtin.Editors;
using Specter.Formatting;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseConsistentIndentation", typeof(Strings), nameof(Strings.UseConsistentIndentationDescription), Severity = DiagnosticSeverity.Warning)]
    internal class UseConsistentIndentation : FormattingRule<UseConsistentIndentationEditorConfiguration>
    {
        internal UseConsistentIndentation(RuleInfo ruleInfo, UseConsistentIndentationEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new UseConsistentIndentationEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.UseConsistentIndentationError;
    }
}
