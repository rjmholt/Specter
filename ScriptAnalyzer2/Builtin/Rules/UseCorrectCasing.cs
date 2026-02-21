using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
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
