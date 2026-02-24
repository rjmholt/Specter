using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Rules.Builtin.Editors
{
    internal sealed class AlignAssignmentStatementEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enable);
        public bool CheckHashtable { get; set; } = true;
    }

    [Editor("AlignAssignmentStatement", Description = "Aligns assignment operators in multi-line hashtables")]
    internal sealed class AlignAssignmentStatementEditor : IScriptEditor, IConfigurableEditor<AlignAssignmentStatementEditorConfiguration>
    {
        internal AlignAssignmentStatementEditor(AlignAssignmentStatementEditorConfiguration configuration)
        {
            Configuration = configuration ?? new AlignAssignmentStatementEditorConfiguration();
        }

        public AlignAssignmentStatementEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();

            if (!Configuration.CheckHashtable) { return edits; }

            var hashtables = ast.FindAll(static a => a is HashtableAst, searchNestedScriptBlocks: true);

            foreach (HashtableAst htAst in hashtables.Cast<HashtableAst>())
            {
                AddEditsForHashtable(htAst, tokens, edits);
            }

            return edits;
        }

        private static void AddEditsForHashtable(HashtableAst htAst, IReadOnlyList<Token> tokens, List<ScriptEdit> edits)
        {
            var pairs = htAst.KeyValuePairs;
            if (pairs == null || pairs.Count < 2)
            {
                return;
            }

            if (htAst.Extent.StartLineNumber == htAst.Extent.EndLineNumber)
            {
                return;
            }

            var assignments = new List<AssignmentInfo>();
            foreach (var pair in pairs)
            {
                ExpressionAst key = pair.Item1;
                StatementAst value = pair.Item2;

                Token? equalsToken = FindEqualsToken(tokens, key.Extent.EndOffset, value.Extent.StartOffset);
                if (equalsToken is null)
                {
                    continue;
                }

                if (key.Extent.EndLineNumber != equalsToken.Extent.StartLineNumber)
                {
                    continue;
                }

                assignments.Add(new AssignmentInfo(key, equalsToken));
            }

            if (assignments.Count < 2)
            {
                return;
            }

            int maxKeyEndColumn = 0;
            foreach (AssignmentInfo info in assignments)
            {
                if (info.Key.Extent.EndColumnNumber > maxKeyEndColumn)
                {
                    maxKeyEndColumn = info.Key.Extent.EndColumnNumber;
                }
            }

            int desiredEqualsColumn = maxKeyEndColumn + 1;

            foreach (AssignmentInfo info in assignments)
            {
                int actualEqualsColumn = info.EqualsToken.Extent.StartColumnNumber;
                if (actualEqualsColumn == desiredEqualsColumn)
                {
                    continue;
                }

                int gapStart = info.Key.Extent.EndOffset;
                int gapEnd = info.EqualsToken.Extent.StartOffset;
                int neededSpaces = desiredEqualsColumn - info.Key.Extent.EndColumnNumber;
                string spaces = new string(' ', Math.Max(1, neededSpaces));

                edits.Add(new ScriptEdit(gapStart, gapEnd, spaces));
            }
        }

        private static Token? FindEqualsToken(IReadOnlyList<Token> tokens, int afterOffset, int beforeOffset)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.Extent.StartOffset < afterOffset) { continue; }
                if (t.Extent.StartOffset >= beforeOffset) { break; }

                if (t.Kind == TokenKind.Equals)
                {
                    return t;
                }
            }

            return null;
        }

        private readonly struct AssignmentInfo
        {
            internal AssignmentInfo(ExpressionAst key, Token equalsToken)
            {
                Key = key;
                EqualsToken = equalsToken;
            }

            public ExpressionAst Key { get; }
            public Token EqualsToken { get; }
        }
    }
}
