using Specter.Builtin.Editors;
using Specter.CommandDatabase;
using Specter.Formatting;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCorrectCasing", typeof(Strings), nameof(Strings.UseCorrectCasingDescription), Severity = DiagnosticSeverity.Information)]
    internal class UseCorrectCasing : FormattingRule<UseCorrectCasingEditorConfiguration>
    {
        private readonly IPowerShellCommandDatabase? _commandDb;

        internal UseCorrectCasing(
            RuleInfo ruleInfo,
            UseCorrectCasingEditorConfiguration configuration,
            IPowerShellCommandDatabase? commandDb)
            : base(ruleInfo, configuration)
        {
            _commandDb = commandDb;
        }

        public override IScriptEditor CreateEditor() => new UseCorrectCasingEditor(Configuration, _commandDb);

        protected override string GetDiagnosticMessage(ScriptEdit edit, string scriptContent)
            => edit.DiagnosticMessage ?? Strings.UseCorrectCasingError;
    }
}
