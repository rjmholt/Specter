using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Rules.Builtin.Editors
{
    internal sealed class PlaceCloseBraceEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enable);
        public bool NoEmptyLineBefore { get; set; } = false;
        public bool IgnoreOneLineBlock { get; set; } = true;
        public bool NewLineAfter { get; set; } = true;
    }

    [Editor("PlaceCloseBrace", Description = "Enforces close brace placement: own line, no empty lines before, newline-after vs cuddled branches")]
    internal sealed class PlaceCloseBraceEditor : IScriptEditor, IConfigurableEditor<PlaceCloseBraceEditorConfiguration>
    {
        internal PlaceCloseBraceEditor(PlaceCloseBraceEditorConfiguration configuration)
        {
            Configuration = configuration ?? new PlaceCloseBraceEditorConfiguration();
        }

        public PlaceCloseBraceEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            var closeToIgnore = BraceHelper.GetCommandElementCloseBraces(ast, tokens);
            var oneLinePairs = new HashSet<int>();

            if (Configuration.IgnoreOneLineBlock)
            {
                foreach (BraceHelper.BracePair pair in BraceHelper.GetBracePairsOnSameLine(ast, tokens))
                {
                    oneLinePairs.Add(pair.CloseIndex);
                }
            }

            var allPairs = BraceHelper.GetAllBracePairs(ast, tokens);
            var closeToOpen = new Dictionary<int, int>();
            foreach (BraceHelper.BracePair pair in allPairs)
            {
                closeToOpen[pair.CloseIndex] = pair.OpenIndex;
            }

            for (int k = 0; k < tokens.Count; k++)
            {
                if (tokens[k].Kind != TokenKind.RCurly)
                {
                    continue;
                }

                if (closeToIgnore.Contains(k) || oneLinePairs.Contains(k))
                {
                    continue;
                }

                if (!closeToOpen.TryGetValue(k, out int openBraceIdx))
                {
                    continue;
                }

                Token openBrace = tokens[openBraceIdx];

                if (openBrace.Kind == TokenKind.AtCurly
                    && openBrace.Extent.StartLineNumber == tokens[k].Extent.StartLineNumber)
                {
                    continue;
                }

                TryAddEditForBraceNotOnOwnLine(tokens, k, openBraceIdx, edits);

                if (Configuration.NoEmptyLineBefore)
                {
                    TryAddEditForEmptyLineBefore(tokens, k, openBraceIdx, edits);
                }

                if (Configuration.NewLineAfter)
                {
                    TryAddEditForMissingNewLineAfter(tokens, k, edits);
                }
                else
                {
                    TryAddEditForUncuddledBranch(tokens, k, edits);
                }
            }

            return edits;
        }

        private static void TryAddEditForBraceNotOnOwnLine(
            IReadOnlyList<Token> tokens, int closeBraceIdx, int openBraceIdx, List<ScriptEdit> edits)
        {
            if (closeBraceIdx < 1 || tokens[closeBraceIdx - 1].Kind == TokenKind.NewLine)
            {
                return;
            }

            string indent = BraceHelper.GetIndentation(tokens, openBraceIdx);
            Token closeBrace = tokens[closeBraceIdx];

            edits.Add(new ScriptEdit(
                closeBrace.Extent.StartOffset,
                closeBrace.Extent.EndOffset,
                Environment.NewLine + indent + "}"));
        }

        private static void TryAddEditForEmptyLineBefore(
            IReadOnlyList<Token> tokens, int closeBraceIdx, int openBraceIdx, List<ScriptEdit> edits)
        {
            if (closeBraceIdx < 2)
            {
                return;
            }

            if (tokens[closeBraceIdx - 1].Kind != TokenKind.NewLine
                || tokens[closeBraceIdx - 2].Kind != TokenKind.NewLine)
            {
                return;
            }

            string indent = BraceHelper.GetIndentation(tokens, openBraceIdx);
            Token extraNewLine = tokens[closeBraceIdx - 2];
            Token closeBrace = tokens[closeBraceIdx];

            edits.Add(new ScriptEdit(
                extraNewLine.Extent.StartOffset,
                closeBrace.Extent.EndOffset,
                extraNewLine.Text + indent + "}",
                diagnosticStartOffset: closeBrace.Extent.StartOffset,
                diagnosticEndOffset: closeBrace.Extent.EndOffset));
        }

        private static void TryAddEditForMissingNewLineAfter(
            IReadOnlyList<Token> tokens, int closeBraceIdx, List<ScriptEdit> edits)
        {
            int nextIdx = closeBraceIdx + 1;
            if (nextIdx >= tokens.Count)
            {
                return;
            }

            if (!BraceHelper.IsBranchKeyword(tokens[nextIdx].Kind))
            {
                return;
            }

            Token closeBrace = tokens[closeBraceIdx];
            edits.Add(new ScriptEdit(
                closeBrace.Extent.StartOffset,
                closeBrace.Extent.EndOffset,
                "}" + Environment.NewLine));
        }

        private static void TryAddEditForUncuddledBranch(
            IReadOnlyList<Token> tokens, int closeBraceIdx, List<ScriptEdit> edits)
        {
            if (closeBraceIdx + 2 >= tokens.Count)
            {
                return;
            }

            Token closeBrace = tokens[closeBraceIdx];
            Token token1 = tokens[closeBraceIdx + 1];
            Token token2 = tokens[closeBraceIdx + 2];

            int branchTokenIdx;

            if (BraceHelper.IsBranchKeyword(token1.Kind)
                && token1.Extent.StartLineNumber == closeBrace.Extent.StartLineNumber
                && token1.Extent.StartColumnNumber - closeBrace.Extent.EndColumnNumber != 1)
            {
                branchTokenIdx = closeBraceIdx + 1;
            }
            else if (token1.Kind == TokenKind.NewLine && BraceHelper.IsBranchKeyword(token2.Kind))
            {
                branchTokenIdx = closeBraceIdx + 2;
            }
            else
            {
                return;
            }

            Token branchToken = tokens[branchTokenIdx];
            edits.Add(new ScriptEdit(
                closeBrace.Extent.StartOffset,
                branchToken.Extent.EndOffset,
                "} " + branchToken.Text));
        }
    }
}
