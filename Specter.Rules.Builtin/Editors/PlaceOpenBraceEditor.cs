using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Rules.Builtin.Editors
{
    internal sealed class PlaceOpenBraceEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enable);
        public bool OnSameLine { get; set; } = true;
        public bool NewLineAfter { get; set; } = true;
        public bool IgnoreOneLineBlock { get; set; } = true;
    }

    [Editor("PlaceOpenBrace", Description = "Enforces open brace placement style (K&R / Allman)")]
    internal sealed class PlaceOpenBraceEditor : IScriptEditor, IConfigurableEditor<PlaceOpenBraceEditorConfiguration>
    {
        internal PlaceOpenBraceEditor(PlaceOpenBraceEditorConfiguration configuration)
        {
            Configuration = configuration ?? new PlaceOpenBraceEditorConfiguration();
        }

        public PlaceOpenBraceEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            var tokensToIgnore = BraceHelper.GetCommandElementOpenBraces(ast, tokens);

            if (Configuration.IgnoreOneLineBlock)
            {
                foreach (BraceHelper.BracePair pair in BraceHelper.GetBracePairsOnSameLine(ast, tokens))
                {
                    tokensToIgnore.Add(pair.OpenIndex);
                }
            }

            for (int k = 0; k < tokens.Count; k++)
            {
                if (tokens[k].Kind != TokenKind.LCurly || tokensToIgnore.Contains(k))
                {
                    continue;
                }

                if (Configuration.OnSameLine)
                {
                    TryAddEditsForBraceOnNewLine(tokens, k, edits);
                }
                else
                {
                    TryAddEditsForBraceOnSameLine(tokens, k, edits);
                }

                if (Configuration.NewLineAfter)
                {
                    TryAddEditForMissingNewLineAfter(tokens, k, edits);
                }
            }

            return edits;
        }

        private static void TryAddEditsForBraceOnNewLine(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k < 2 || tokens[k - 1].Kind != TokenKind.NewLine)
            {
                return;
            }

            Token precedingExpr = tokens[k - 2];
            string commentSuffix = string.Empty;

            if (precedingExpr.Kind == TokenKind.Comment && k > 2)
            {
                commentSuffix = " " + precedingExpr.Text;
                precedingExpr = tokens[k - 3];
            }

            edits.Add(new ScriptEdit(
                precedingExpr.Extent.EndOffset,
                tokens[k].Extent.EndOffset,
                " {" + commentSuffix,
                diagnosticStartOffset: tokens[k].Extent.StartOffset,
                diagnosticEndOffset: tokens[k].Extent.EndOffset));
        }

        private static void TryAddEditsForBraceOnSameLine(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k < 1 || tokens[k - 1].Kind == TokenKind.NewLine)
            {
                return;
            }

            string indent = BraceHelper.GetIndentation(tokens, k);
            int prevEnd = tokens[k - 1].Extent.EndOffset;

            edits.Add(new ScriptEdit(
                prevEnd,
                tokens[k].Extent.EndOffset,
                Environment.NewLine + indent + "{",
                diagnosticStartOffset: tokens[k].Extent.StartOffset,
                diagnosticEndOffset: tokens[k].Extent.EndOffset));
        }

        private static void TryAddEditForMissingNewLineAfter(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k + 1 >= tokens.Count)
            {
                return;
            }

            TokenKind nextKind = tokens[k + 1].Kind;
            if (nextKind == TokenKind.NewLine || nextKind == TokenKind.EndOfInput)
            {
                return;
            }

            int insertAt = tokens[k].Extent.EndOffset;
            edits.Add(new ScriptEdit(
                insertAt, insertAt, Environment.NewLine,
                diagnosticStartOffset: tokens[k].Extent.StartOffset,
                diagnosticEndOffset: tokens[k].Extent.EndOffset));
        }
    }
}
