using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace PSpecter.Builtin.Editors
{
    /// <summary>
    /// Shared utilities for brace placement analysis.
    /// Used by both PlaceOpenBraceEditor and PlaceCloseBraceEditor.
    /// </summary>
    internal static class BraceHelper
    {
        public static HashSet<int> GetCommandElementOpenBraces(Ast ast, IReadOnlyList<Token> tokens)
        {
            var result = new HashSet<int>();
            var cmdElemScriptBlocks = ast.FindAll(
                a => a is ScriptBlockExpressionAst && a.Parent is CommandAst,
                searchNestedScriptBlocks: true);

            foreach (Ast sbAst in cmdElemScriptBlocks)
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (tokens[i].Kind == TokenKind.LCurly
                        && tokens[i].Extent.StartOffset == sbAst.Extent.StartOffset)
                    {
                        result.Add(i);
                        break;
                    }
                }
            }

            return result;
        }

        public static HashSet<int> GetCommandElementCloseBraces(Ast ast, IReadOnlyList<Token> tokens)
        {
            var result = new HashSet<int>();
            var cmdElemScriptBlocks = ast.FindAll(
                a => a is ScriptBlockExpressionAst && a.Parent is CommandAst,
                searchNestedScriptBlocks: true);

            foreach (Ast sbAst in cmdElemScriptBlocks)
            {
                for (int i = tokens.Count - 1; i >= 0; i--)
                {
                    if (tokens[i].Kind == TokenKind.RCurly
                        && tokens[i].Extent.EndOffset == sbAst.Extent.EndOffset)
                    {
                        result.Add(i);
                        break;
                    }
                }
            }

            return result;
        }

        public static List<BracePair> GetBracePairsOnSameLine(Ast ast, IReadOnlyList<Token> tokens)
        {
            var result = new List<BracePair>();

            var braceStack = new Stack<int>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LCurly || tokens[i].Kind == TokenKind.AtCurly)
                {
                    braceStack.Push(i);
                }
                else if (tokens[i].Kind == TokenKind.RCurly && braceStack.Count > 0)
                {
                    int openIdx = braceStack.Pop();

                    if (tokens[openIdx].Extent.StartLineNumber == tokens[i].Extent.StartLineNumber)
                    {
                        result.Add(new BracePair(openIdx, i));
                    }
                }
            }

            return result;
        }

        public static List<BracePair> GetAllBracePairs(Ast ast, IReadOnlyList<Token> tokens)
        {
            var result = new List<BracePair>();

            var braceStack = new Stack<int>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LCurly || tokens[i].Kind == TokenKind.AtCurly)
                {
                    braceStack.Push(i);
                }
                else if (tokens[i].Kind == TokenKind.RCurly && braceStack.Count > 0)
                {
                    int openIdx = braceStack.Pop();
                    result.Add(new BracePair(openIdx, i));
                }
            }

            return result;
        }

        public static string GetIndentation(IReadOnlyList<Token> tokens, int openBraceIndex)
        {
            Token openBrace = tokens[openBraceIndex];

            if (openBraceIndex > 0 && tokens[openBraceIndex - 1].Kind == TokenKind.NewLine)
            {
                return new string(' ', openBrace.Extent.StartColumnNumber - 1);
            }

            int line = openBrace.Extent.StartLineNumber;
            Token firstOnLine = openBrace;
            for (int i = openBraceIndex - 1; i >= 0; i--)
            {
                if (tokens[i].Extent.StartLineNumber != line)
                {
                    break;
                }
                firstOnLine = tokens[i];
            }

            return new string(' ', firstOnLine.Extent.StartColumnNumber - 1);
        }

        public static bool IsBranchKeyword(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Else:
                case TokenKind.ElseIf:
                case TokenKind.Catch:
                case TokenKind.Finally:
                    return true;
                default:
                    return false;
            }
        }

        internal readonly struct BracePair
        {
            public BracePair(int openIndex, int closeIndex)
            {
                OpenIndex = openIndex;
                CloseIndex = closeIndex;
            }

            public int OpenIndex { get; }
            public int CloseIndex { get; }
        }
    }
}
