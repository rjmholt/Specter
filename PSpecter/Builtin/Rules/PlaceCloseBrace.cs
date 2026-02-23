using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PlaceCloseBrace", typeof(Strings), nameof(Strings.PlaceCloseBraceDescription), Severity = DiagnosticSeverity.Warning)]
    internal class PlaceCloseBrace : FormattingRule<PlaceCloseBraceEditorConfiguration>
    {
        internal PlaceCloseBrace(RuleInfo ruleInfo, PlaceCloseBraceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new PlaceCloseBraceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.PlaceCloseBraceErrorShouldBeOnNewLine;
    }
}
