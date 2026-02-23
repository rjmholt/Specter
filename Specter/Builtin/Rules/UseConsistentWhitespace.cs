using Specter.Builtin.Editors;
using Specter.Formatting;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseConsistentWhitespace", typeof(Strings), nameof(Strings.UseConsistentWhitespaceDescription), Severity = DiagnosticSeverity.Warning)]
    internal class UseConsistentWhitespace : FormattingRule<UseConsistentWhitespaceEditorConfiguration>
    {
        internal UseConsistentWhitespace(RuleInfo ruleInfo, UseConsistentWhitespaceEditorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IScriptEditor CreateEditor() => new UseConsistentWhitespaceEditor(Configuration);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => Strings.UseConsistentWhitespaceErrorBeforeOpeningBrace;
    }
}
