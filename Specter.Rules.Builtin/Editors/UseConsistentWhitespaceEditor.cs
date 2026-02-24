using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Rules.Builtin.Editors
{
    internal sealed class UseConsistentWhitespaceEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enable);
        public bool CheckOpenBrace { get; set; } = true;
        public bool CheckInnerBrace { get; set; } = true;
        public bool CheckPipe { get; set; } = true;
        public bool CheckPipeForRedundantWhitespace { get; set; } = false;
        public bool CheckOpenParen { get; set; } = true;
        public bool CheckOperator { get; set; } = true;
        public bool CheckSeparator { get; set; } = true;
        public bool CheckParameter { get; set; } = false;
        public bool IgnoreAssignmentOperatorInsideHashTable { get; set; } = true;
    }

    [Editor("UseConsistentWhitespace", Description = "Enforces consistent whitespace around operators, braces, pipes, keywords, and separators")]
    internal sealed class UseConsistentWhitespaceEditor : IScriptEditor, IConfigurableEditor<UseConsistentWhitespaceEditorConfiguration>
    {
        internal UseConsistentWhitespaceEditor(UseConsistentWhitespaceEditorConfiguration configuration)
        {
            Configuration = configuration ?? new UseConsistentWhitespaceEditorConfiguration();
        }

        public UseConsistentWhitespaceEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            var multiLineHashtableRanges = Configuration.IgnoreAssignmentOperatorInsideHashTable
                ? GetMultiLineHashtableRanges(ast)
                : null;
            var bracedMemberAccessRanges = Configuration.CheckInnerBrace
                ? GetBracedMemberAccessRanges(tokens)
                : null;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (Configuration.CheckOpenBrace && token.Kind == TokenKind.LCurly)
                {
                    AddSpaceBeforeOpenBrace(tokens, i, edits);
                }

                if (Configuration.CheckInnerBrace)
                {
                    if (token.Kind == TokenKind.LCurly
                        && !IsInsideBracedMemberAccess(token, bracedMemberAccessRanges!))
                    {
                        AddSpaceAfterOpenBrace(tokens, i, edits);
                    }
                    else if (token.Kind == TokenKind.RCurly
                        && !IsInsideBracedMemberAccess(token, bracedMemberAccessRanges!))
                    {
                        AddSpaceBeforeCloseBrace(tokens, i, edits);
                    }
                }

                if ((Configuration.CheckPipe || Configuration.CheckPipeForRedundantWhitespace) && token.Kind == TokenKind.Pipe)
                {
                    AddSpaceAroundPipe(tokens, i, edits);
                }

                if (Configuration.CheckOpenParen && token.Kind == TokenKind.LParen)
                {
                    AddSpaceBetweenKeywordAndParen(tokens, i, edits);
                }

                if (Configuration.CheckOperator && IsOperator(token))
                {
                    AddSpaceAroundOperator(tokens, i, edits, multiLineHashtableRanges);
                }

                if (Configuration.CheckSeparator && (token.Kind == TokenKind.Comma || token.Kind == TokenKind.Semi))
                {
                    AddSpaceAfterSeparator(tokens, i, edits);
                }
            }

            if (Configuration.CheckParameter)
            {
                AddParameterSpacingEdits(ast, tokens, edits);
            }

            return edits;
        }

        private void AddSpaceBeforeOpenBrace(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k < 1) { return; }

            Token prev = tokens[k - 1];
            Token brace = tokens[k];

            if (prev.Kind == TokenKind.NewLine
                || prev.Kind == TokenKind.LCurly
                || prev.Kind == TokenKind.LParen
                || prev.Kind == TokenKind.DotDot
                || prev.Kind == TokenKind.Dot
#if CORECLR
                || prev.Kind == TokenKind.QuestionDot
#endif
                || prev.Extent.StartLineNumber != brace.Extent.StartLineNumber)
            {
                return;
            }

            if ((prev.TokenFlags & TokenFlags.MemberName) != 0)
            {
                return;
            }

            EnsureOneSpaceBetween(prev, brace, edits);
        }

        private static void AddSpaceAfterOpenBrace(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k + 1 >= tokens.Count) { return; }

            Token brace = tokens[k];
            Token next = tokens[k + 1];

            if (next.Kind == TokenKind.NewLine
                || next.Kind == TokenKind.EndOfInput
                || next.Kind == TokenKind.LineContinuation
                || next.Kind == TokenKind.RCurly
                || next.Extent.StartLineNumber != brace.Extent.StartLineNumber)
            {
                return;
            }

            EnsureOneSpaceBetween(brace, next, edits);
        }

        private static void AddSpaceBeforeCloseBrace(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k < 1) { return; }

            Token prev = tokens[k - 1];
            Token brace = tokens[k];

            if (prev.Kind == TokenKind.NewLine
                || prev.Kind == TokenKind.LCurly
                || prev.Kind == TokenKind.AtCurly
                || prev.Kind == TokenKind.LineContinuation
                || prev.Extent.StartLineNumber != brace.Extent.StartLineNumber)
            {
                return;
            }

            EnsureOneSpaceBetween(prev, brace, edits);
        }

        private void AddSpaceAroundPipe(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            Token pipe = tokens[k];

            if (k > 0)
            {
                Token prev = tokens[k - 1];
                if (prev.Kind != TokenKind.NewLine
                    && prev.Kind != TokenKind.LineContinuation
                    && prev.Kind != TokenKind.Pipe
                    && prev.Extent.StartLineNumber == pipe.Extent.StartLineNumber)
                {
                    int spacing = pipe.Extent.StartOffset - prev.Extent.EndOffset;

                    if (Configuration.CheckPipe && spacing == 0)
                    {
                        EnsureOneSpaceBetween(prev, pipe, edits);
                    }
                    else if (Configuration.CheckPipeForRedundantWhitespace && spacing > 1)
                    {
                        EnsureOneSpaceBetween(prev, pipe, edits);
                    }
                }
            }

            if (k + 1 < tokens.Count)
            {
                Token next = tokens[k + 1];
                if (next.Kind != TokenKind.NewLine
                    && next.Kind != TokenKind.LineContinuation
                    && next.Kind != TokenKind.Pipe
                    && next.Kind != TokenKind.EndOfInput
                    && next.Extent.StartLineNumber == pipe.Extent.StartLineNumber)
                {
                    int spacing = next.Extent.StartOffset - pipe.Extent.EndOffset;

                    if (Configuration.CheckPipe && spacing == 0)
                    {
                        EnsureOneSpaceBetween(pipe, next, edits);
                    }
                    else if (Configuration.CheckPipeForRedundantWhitespace && spacing > 1)
                    {
                        EnsureOneSpaceBetween(pipe, next, edits);
                    }
                }
            }
        }

        private static void AddSpaceBetweenKeywordAndParen(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k < 1) { return; }

            Token prev = tokens[k - 1];
            Token paren = tokens[k];

            if (prev.Extent.StartLineNumber != paren.Extent.StartLineNumber)
            {
                return;
            }

            if (!IsKeywordBeforeParen(prev.Kind))
            {
                return;
            }

            EnsureOneSpaceBetween(prev, paren, edits);
        }

        private void AddSpaceAroundOperator(
            IReadOnlyList<Token> tokens,
            int k,
            List<ScriptEdit> edits,
            List<OffsetRange>? multiLineHashtableRanges)
        {
            Token op = tokens[k];

            if (op.Kind == TokenKind.DotDot)
            {
                return;
            }

            if (Configuration.IgnoreAssignmentOperatorInsideHashTable
                && (op.TokenFlags & TokenFlags.AssignmentOperator) != 0
                && multiLineHashtableRanges != null
                && IsInsideMultiLineHashtable(op.Extent.StartOffset, tokens, k, multiLineHashtableRanges))
            {
                return;
            }

            if (k > 0 && tokens[k - 1].Kind == TokenKind.LParen
                && (op.TokenFlags & TokenFlags.UnaryOperator) != 0)
            {
                return;
            }

            bool needSpaceBefore = false;
            bool needSpaceAfter = false;

            if (k > 0)
            {
                Token prev = tokens[k - 1];
                if (prev.Kind != TokenKind.NewLine
                    && prev.Kind != TokenKind.LineContinuation
                    && prev.Extent.StartLineNumber == op.Extent.StartLineNumber)
                {
                    int gap = op.Extent.StartOffset - prev.Extent.EndOffset;
                    needSpaceBefore = gap != 1;
                }
            }

            if (k + 1 < tokens.Count)
            {
                Token next = tokens[k + 1];
                if (next.Kind != TokenKind.NewLine
                    && next.Kind != TokenKind.LineContinuation
                    && next.Kind != TokenKind.EndOfInput
                    && next.Extent.StartLineNumber == op.Extent.StartLineNumber)
                {
                    int gap = next.Extent.StartOffset - op.Extent.EndOffset;
                    needSpaceAfter = gap != 1;
                }
            }

            if (!needSpaceBefore && !needSpaceAfter)
            {
                return;
            }

            if (needSpaceBefore && needSpaceAfter)
            {
                Token prev = tokens[k - 1];
                Token next = tokens[k + 1];
                edits.Add(new ScriptEdit(
                    prev.Extent.EndOffset,
                    next.Extent.StartOffset,
                    " " + op.Text + " "));
            }
            else if (needSpaceBefore)
            {
                Token prev = tokens[k - 1];
                EnsureOneSpaceBetween(prev, op, edits);
            }
            else
            {
                Token next = tokens[k + 1];
                EnsureOneSpaceBetween(op, next, edits);
            }
        }

        private static void AddSpaceAfterSeparator(IReadOnlyList<Token> tokens, int k, List<ScriptEdit> edits)
        {
            if (k + 1 >= tokens.Count) { return; }

            Token sep = tokens[k];
            Token next = tokens[k + 1];

            if (next.Kind == TokenKind.NewLine
                || next.Kind == TokenKind.EndOfInput
                || next.Kind == TokenKind.Comment
                || next.Kind == TokenKind.LineContinuation
                || next.Extent.StartLineNumber != sep.Extent.StartLineNumber)
            {
                return;
            }

            EnsureOneSpaceBetween(sep, next, edits);
        }

        private static void AddParameterSpacingEdits(Ast ast, IReadOnlyList<Token> tokens, List<ScriptEdit> edits)
        {
            foreach (Ast node in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;

                var sortedElements = new List<Ast>();
                foreach (Ast child in cmdAst.FindAll(testAst => testAst.Parent == cmdAst, searchNestedScriptBlocks: false))
                {
                    if (child is RedirectionAst)
                    {
                        continue;
                    }

                    sortedElements.Add(child);
                }

                sortedElements.Sort(static (a, b) =>
                {
                    int cmp = a.Extent.StartLineNumber.CompareTo(b.Extent.StartLineNumber);
                    return cmp != 0 ? cmp : a.Extent.StartColumnNumber.CompareTo(b.Extent.StartColumnNumber);
                });

                for (int i = 0; i < sortedElements.Count - 1; i++)
                {
                    Ast current = sortedElements[i];
                    Ast next = sortedElements[i + 1];

                    if (current.Extent.EndLineNumber != next.Extent.StartLineNumber)
                    {
                        continue;
                    }

                    int gap = next.Extent.StartOffset - current.Extent.EndOffset;
                    if (gap > 1)
                    {
                        edits.Add(new ScriptEdit(
                            current.Extent.EndOffset + 1,
                            next.Extent.StartOffset,
                            string.Empty,
                            diagnosticStartOffset: current.Extent.StartOffset,
                            diagnosticEndOffset: current.Extent.EndOffset));
                    }
                }

                AddRedirectSpacingEdits(cmdAst, tokens, edits);
            }
        }

        private static void AddRedirectSpacingEdits(CommandAst cmdAst, IReadOnlyList<Token> tokens, List<ScriptEdit> edits)
        {
            if (cmdAst.Redirections == null || cmdAst.Redirections.Count == 0)
            {
                return;
            }

            int cmdStartOffset = cmdAst.Extent.StartOffset;
            int cmdEndOffset = cmdAst.Extent.EndOffset;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.Extent.StartOffset < cmdStartOffset) continue;
                if (t.Extent.StartOffset >= cmdEndOffset) break;

                if (t.Kind != TokenKind.Redirection && t.Kind != TokenKind.RedirectInStd) continue;

                if (i > 0)
                {
                    Token prev = tokens[i - 1];
                    if (prev.Extent.StartLineNumber == t.Extent.StartLineNumber)
                    {
                        int gap = t.Extent.StartOffset - prev.Extent.EndOffset;
                        if (gap > 1)
                        {
                            edits.Add(new ScriptEdit(
                                prev.Extent.EndOffset + 1,
                                t.Extent.StartOffset,
                                string.Empty,
                                diagnosticStartOffset: prev.Extent.StartOffset,
                                diagnosticEndOffset: prev.Extent.EndOffset));
                        }
                    }
                }
            }
        }

        private static void EnsureOneSpaceBetween(Token left, Token right, List<ScriptEdit> edits)
        {
            int gap = right.Extent.StartOffset - left.Extent.EndOffset;
            if (gap == 1)
            {
                return;
            }

            edits.Add(new ScriptEdit(left.Extent.EndOffset, right.Extent.StartOffset, " "));
        }

        private static bool IsOperator(Token token)
        {
            return (token.TokenFlags & TokenFlags.AssignmentOperator) != 0
                || (token.TokenFlags & TokenFlags.BinaryOperator) != 0
                || token.Kind == TokenKind.AndAnd
                || token.Kind == TokenKind.OrOr;
        }

        private static bool IsKeywordBeforeParen(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.If:
                case TokenKind.ElseIf:
                case TokenKind.Switch:
                case TokenKind.For:
                case TokenKind.Foreach:
                case TokenKind.While:
                    return true;
                default:
                    return false;
            }
        }

        private static List<HashtableAst> GetAllHashtables(Ast ast)
        {
            var hashtables = new List<HashtableAst>();
            foreach (Ast htAst in ast.FindAll(static a => a is HashtableAst, searchNestedScriptBlocks: true))
            {
                hashtables.Add((HashtableAst)htAst);
            }
            return hashtables;
        }

        private static List<OffsetRange> GetMultiLineHashtableRanges(Ast ast)
        {
            var ranges = new List<OffsetRange>();
            foreach (HashtableAst htAst in GetAllHashtables(ast))
            {
                if (htAst.Extent.StartLineNumber != htAst.Extent.EndLineNumber)
                {
                    ranges.Add(new OffsetRange(htAst.Extent.StartOffset, htAst.Extent.EndOffset));
                }
            }
            return ranges;
        }

        /// <summary>
        /// Returns true if the operator is directly inside a multi-line hashtable.
        /// If a single-line hashtable is nested within a multi-line one, assignments in the
        /// single-line hashtable are NOT considered "in a multi-line hashtable".
        /// </summary>
        private static bool IsInsideMultiLineHashtable(
            int offset, IReadOnlyList<Token> tokens, int tokenIndex, List<OffsetRange> multiLineRanges)
        {
            if (!IsInsideRange(offset, multiLineRanges))
            {
                return false;
            }

            int singleLineDepth = 0;
            for (int i = tokenIndex - 1; i >= 0; i--)
            {
                if (tokens[i].Kind == TokenKind.RCurly)
                {
                    singleLineDepth++;
                }
                else if (tokens[i].Kind == TokenKind.LCurly || tokens[i].Kind == TokenKind.AtCurly)
                {
                    if (singleLineDepth > 0)
                    {
                        singleLineDepth--;
                        continue;
                    }

                    bool isMultiLine = tokens[i].Extent.StartLineNumber != FindMatchingRCurlyLine(tokens, i);
                    return isMultiLine;
                }
            }

            return true;
        }

        private static int FindMatchingRCurlyLine(IReadOnlyList<Token> tokens, int lCurlyIdx)
        {
            int depth = 1;
            for (int i = lCurlyIdx + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LCurly || tokens[i].Kind == TokenKind.AtCurly)
                {
                    depth++;
                }
                else if (tokens[i].Kind == TokenKind.RCurly)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return tokens[i].Extent.StartLineNumber;
                    }
                }
            }
            return -1;
        }

        private static bool IsInsideRange(int offset, List<OffsetRange> ranges)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                if (offset >= ranges[i].Start && offset < ranges[i].End)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsInsideBracedMemberAccess(Token token, List<OffsetRange> ranges)
        {
            int offset = token.Extent.StartOffset;
            for (int i = 0; i < ranges.Count; i++)
            {
                if (offset >= ranges[i].Start && offset < ranges[i].End)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Identifies ranges in the token stream that represent braced member access
        /// expressions like <c>$obj.{PropertyName}</c>. The contents of such braces
        /// are literal member names and should not be reformatted.
        /// </summary>
        private static List<OffsetRange> GetBracedMemberAccessRanges(IReadOnlyList<Token> tokens)
        {
            var ranges = new List<OffsetRange>();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];

                if (t.Kind != TokenKind.Dot
#if CORECLR
                    && t.Kind != TokenKind.QuestionDot
#endif
                    )
                {
                    continue;
                }

                if (i == 0)
                {
                    continue;
                }

                int leftIdx = i - 1;
                while (leftIdx > 0 && tokens[leftIdx].Kind == TokenKind.Comment)
                {
                    leftIdx--;
                }

                Token left = tokens[leftIdx];

                // Verify the chain from left through any intervening comments to the
                // dot is contiguous (no whitespace gaps). This avoids false positives
                // with dot-sourcing like `$a .{blah}`.
                bool contiguous = true;
                for (int c = leftIdx; c < i; c++)
                {
                    if (tokens[c].Extent.EndOffset != tokens[c + 1].Extent.StartOffset)
                    {
                        contiguous = false;
                        break;
                    }
                }

                if (!contiguous)
                {
                    continue;
                }

                switch (left.Kind)
                {
                    case TokenKind.Variable:
                    case TokenKind.Identifier:
                    case TokenKind.StringLiteral:
                    case TokenKind.StringExpandable:
                    case TokenKind.HereStringLiteral:
                    case TokenKind.HereStringExpandable:
                    case TokenKind.RParen:
                    case TokenKind.RCurly:
                    case TokenKind.RBracket:
                        break;
                    default:
                        continue;
                }

                int scan = i + 1;
                while (scan < tokens.Count)
                {
                    TokenKind sk = tokens[scan].Kind;
                    if (sk == TokenKind.Comment || sk == TokenKind.NewLine || sk == TokenKind.LineContinuation)
                    {
                        scan++;
                        continue;
                    }

                    break;
                }

                if (scan >= tokens.Count || tokens[scan].Kind != TokenKind.LCurly)
                {
                    continue;
                }

                int lCurlyIdx = scan;
                int depth = 0;
                int rCurlyIdx = -1;
                for (int j = lCurlyIdx; j < tokens.Count; j++)
                {
                    if (tokens[j].Kind == TokenKind.LCurly)
                    {
                        depth++;
                    }
                    else if (tokens[j].Kind == TokenKind.RCurly)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            rCurlyIdx = j;
                            break;
                        }
                    }
                }

                if (rCurlyIdx < 0)
                {
                    continue;
                }

                ranges.Add(new OffsetRange(
                    tokens[lCurlyIdx].Extent.StartOffset,
                    tokens[rCurlyIdx].Extent.EndOffset));

                i = rCurlyIdx;
            }

            return ranges;
        }

        private readonly struct OffsetRange
        {
            internal OffsetRange(int start, int end) { Start = start; End = end; }
            public int Start { get; }
            public int End { get; }
        }
    }
}
