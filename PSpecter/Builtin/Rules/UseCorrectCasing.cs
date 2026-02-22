using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCorrectCasing", typeof(Strings), nameof(Strings.UseCorrectCasingDescription), Severity = DiagnosticSeverity.Information)]
    public class UseCorrectCasing : FormattingRule<UseCorrectCasingEditorConfiguration>
    {
        public UseCorrectCasing(RuleInfo ruleInfo, UseCorrectCasingEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new UseCorrectCasingEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.UseCorrectCasingError;
    }
}
