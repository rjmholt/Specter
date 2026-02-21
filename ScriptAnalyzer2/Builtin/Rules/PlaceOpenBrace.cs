using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PlaceOpenBrace", typeof(Strings), nameof(Strings.PlaceOpenBraceDescription), Severity = DiagnosticSeverity.Warning)]
    public class PlaceOpenBrace : FormattingRule<PlaceOpenBraceEditorConfiguration>
    {
        public PlaceOpenBrace(RuleInfo ruleInfo, PlaceOpenBraceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new PlaceOpenBraceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.PlaceOpenBraceErrorShouldBeOnSameLine;
    }
}
