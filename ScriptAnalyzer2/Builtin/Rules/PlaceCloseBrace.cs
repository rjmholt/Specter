using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PlaceCloseBrace", typeof(Strings), nameof(Strings.PlaceCloseBraceDescription), Severity = DiagnosticSeverity.Warning)]
    public class PlaceCloseBrace : FormattingRule<PlaceCloseBraceEditorConfiguration>
    {
        public PlaceCloseBrace(RuleInfo ruleInfo, PlaceCloseBraceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new PlaceCloseBraceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.PlaceCloseBraceErrorShouldBeOnNewLine;
    }
}
