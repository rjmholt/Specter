using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Formatting;

namespace PSpecter.Builtin.Editors
{
    public sealed class UseCorrectCasingEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enabled);
        public bool CheckKeyword { get; set; } = true;
        public bool CheckOperator { get; set; } = true;
    }

    [Editor("UseCorrectCasing", Description = "Normalizes casing for PowerShell keywords and operators to lowercase")]
    public sealed class UseCorrectCasingEditor : IScriptEditor, IConfigurableEditor<UseCorrectCasingEditorConfiguration>
    {
        public UseCorrectCasingEditor(UseCorrectCasingEditorConfiguration configuration)
        {
            Configuration = configuration ?? new UseCorrectCasingEditorConfiguration();
        }

        public UseCorrectCasingEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (Configuration.CheckKeyword && (token.TokenFlags & TokenFlags.Keyword) != 0)
                {
                    TryAddCasingEdit(token, edits);
                }
                else if (Configuration.CheckOperator && IsOperator(token))
                {
                    TryAddCasingEdit(token, edits);
                }
            }

            return edits;
        }

        private static void TryAddCasingEdit(Token token, List<ScriptEdit> edits)
        {
            string text = token.Text;
            string lower = text.ToLowerInvariant();

            if (text != lower)
            {
                edits.Add(new ScriptEdit(
                    token.Extent.StartOffset,
                    token.Extent.EndOffset,
                    lower));
            }
        }

        private static bool IsOperator(Token token)
        {
            return (token.TokenFlags & TokenFlags.BinaryOperator) != 0
                || (token.TokenFlags & TokenFlags.UnaryOperator) != 0;
        }
    }
}
