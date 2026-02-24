using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Formatting;

namespace Specter.Rules
{
    /// <summary>
    /// Base class for formatting rules that produce diagnostics by running their
    /// associated <see cref="IScriptEditor"/> and converting each <see cref="ScriptEdit"/>
    /// into a <see cref="ScriptDiagnostic"/> with a correction.
    /// </summary>
    public abstract class FormattingRule<TConfiguration> : ConfigurableScriptRule<TConfiguration>, IFormattingRule
        where TConfiguration : IEditorConfiguration
    {
        protected FormattingRule(RuleInfo ruleInfo, TConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public abstract IScriptEditor CreateEditor();

        /// <summary>
        /// Returns a diagnostic message for a given edit. Override for rule-specific messages.
        /// </summary>
        protected abstract string GetDiagnosticMessage(ScriptEdit edit, string scriptContent);

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (!Configuration.Common.Enable)
            {
                yield break;
            }

            string scriptContent = ast.Extent.Text;
            IScriptEditor editor = CreateEditor();
            IReadOnlyList<ScriptEdit> edits = editor.GetEdits(scriptContent, ast, tokens, scriptPath);

            for (int i = 0; i < edits.Count; i++)
            {
                ScriptEdit edit = edits[i];
                string message = GetDiagnosticMessage(edit, scriptContent);

                ScriptExtent editExtent = ScriptExtent.FromOffsets(scriptContent, scriptPath, edit.StartOffset, edit.EndOffset);
                var correction = new Correction(editExtent, edit.NewText, message);

                ScriptExtent diagnosticExtent = edit.HasDiagnosticExtent
                    ? ScriptExtent.FromOffsets(scriptContent, scriptPath, edit.DiagnosticStartOffset, edit.DiagnosticEndOffset)
                    : editExtent;

                yield return CreateDiagnostic(message, diagnosticExtent, new[] { correction });
            }
        }
    }
}
