using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseConsistentWhitespace", typeof(Strings), nameof(Strings.UseConsistentWhitespaceDescription), Severity = DiagnosticSeverity.Warning)]
    public class UseConsistentWhitespace : FormattingRule<UseConsistentWhitespaceEditorConfiguration>
    {
        public UseConsistentWhitespace(RuleInfo ruleInfo, UseConsistentWhitespaceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new UseConsistentWhitespaceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.UseConsistentWhitespaceErrorBeforeOpeningBrace;
    }
}
