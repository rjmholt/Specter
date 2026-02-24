using Specter.Rules.Builtin.Editors;
using Specter.Formatting;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PlaceOpenBrace", typeof(Strings), nameof(Strings.PlaceOpenBraceDescription), Severity = DiagnosticSeverity.Warning)]
    internal class PlaceOpenBrace : FormattingRule<PlaceOpenBraceEditorConfiguration>
    {
        internal PlaceOpenBrace(RuleInfo ruleInfo, PlaceOpenBraceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new PlaceOpenBraceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.PlaceOpenBraceErrorShouldBeOnSameLine;
    }
}
