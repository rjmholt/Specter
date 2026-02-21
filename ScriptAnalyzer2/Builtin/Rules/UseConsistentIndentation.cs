using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
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
