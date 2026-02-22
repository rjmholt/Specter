using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Formatting;

namespace PSpecter.Builtin.Editors
{
    public sealed class UseConsistentWhitespaceEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enabled);
        public bool CheckOpenBrace { get; set; } = true;
        public bool CheckInnerBrace { get; set; } = true;
        public bool CheckPipe { get; set; } = true;
        public bool CheckPipeForRedundantWhitespace { get; set; } = false;
        public bool CheckOpenParen { get; set; } = true;
        public bool CheckOperator { get; set; } = true;
        public bool CheckSeparator { get; set; } = true;
        public bool IgnoreAssignmentOperatorInsideHashTable { get; set; } = true;
    }

    [Editor("UseConsistentWhitespace", Description = "Enforces consistent whitespace around operators, braces, pipes, keywords, and separators")]
    public sealed class UseConsistentWhitespaceEditor : IScriptEditor, IConfigurableEditor<UseConsistentWhitespaceEditorConfiguration>
    {
        public UseConsistentWhitespaceEditor(UseConsistentWhitespaceEditorConfiguration configuration)
        {
            Configuration = configuration ?? new UseConsistentWhitespaceEditorConfiguration();
        }

        public UseConsistentWhitespaceEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            var multiLineHashtableRanges = Configuration.IgnoreAssignmentOperatorInsideHashTable
                ? GetMultiLineHashtableRanges(ast)
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
                    if (token.Kind == TokenKind.LCurly)
                    {
                        AddSpaceAfterOpenBrace(tokens, i, edits);
                    }
                    else if (token.Kind == TokenKind.RCurly)
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
            List<OffsetRange> multiLineHashtableRanges)
        {
            Token op = tokens[k];

            if (op.Kind == TokenKind.DotDot)
            {
                return;
            }

            if (Configuration.IgnoreAssignmentOperatorInsideHashTable
                && (op.TokenFlags & TokenFlags.AssignmentOperator) != 0
                && multiLineHashtableRanges != null
                && IsInsideRange(op.Extent.StartOffset, multiLineHashtableRanges))
            {
                return;
            }

            if (k > 0 && tokens[k - 1].Kind == TokenKind.LParen
                && (op.TokenFlags & TokenFlags.UnaryOperator) != 0)
            {
                return;
            }

            if (k > 0)
            {
                Token prev = tokens[k - 1];
                if (prev.Kind != TokenKind.NewLine
                    && prev.Kind != TokenKind.LineContinuation
                    && prev.Extent.StartLineNumber == op.Extent.StartLineNumber)
                {
                    EnsureOneSpaceBetween(prev, op, edits);
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
                    EnsureOneSpaceBetween(op, next, edits);
                }
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

        private static List<OffsetRange> GetMultiLineHashtableRanges(Ast ast)
        {
            var ranges = new List<OffsetRange>();
            foreach (Ast htAst in ast.FindAll(a => a is HashtableAst, searchNestedScriptBlocks: true))
            {
                if (htAst.Extent.StartLineNumber != htAst.Extent.EndLineNumber)
                {
                    ranges.Add(new OffsetRange(htAst.Extent.StartOffset, htAst.Extent.EndOffset));
                }
            }
            return ranges;
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

        private readonly struct OffsetRange
        {
            public OffsetRange(int start, int end) { Start = start; End = end; }
            public int Start { get; }
            public int End { get; }
        }
    }
}
