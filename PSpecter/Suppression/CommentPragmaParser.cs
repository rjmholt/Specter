using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter.Suppression
{
    /// <summary>
    /// Parses comment tokens for suppression pragmas:
    /// - Line: <c># pspecter-suppress RuleName</c> (suppresses next non-comment line)
    /// - Inline: <c>$x = 1 # pspecter-suppress RuleName</c> (suppresses current line)
    /// - Block begin: <c># pspecter-suppress-begin RuleName</c>
    /// - Block end: <c># pspecter-suppress-end RuleName</c>
    /// </summary>
    public static class CommentPragmaParser
    {
        private const string PragmaPrefix = "pspecter-suppress";
        private const string BlockBeginPrefix = "pspecter-suppress-begin";
        private const string BlockEndPrefix = "pspecter-suppress-end";

        public static void CollectFromTokens(
            Token[] tokens,
            Dictionary<string, List<RuleSuppression>> suppressions)
        {
            if (tokens is null || tokens.Length == 0)
            {
                return;
            }

            var blockStarts = new Dictionary<string, Stack<Token>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tokens.Length; i++)
            {
                Token token = tokens[i];
                if (token.Kind != TokenKind.Comment)
                {
                    continue;
                }

                string text = token.Text;
                if (text is null || text.Length < 2)
                {
                    continue;
                }

                string body = text.StartsWith("<#", StringComparison.Ordinal)
                    ? text.Substring(2, Math.Max(0, text.Length - 4)).Trim()
                    : text.Substring(1).Trim();

                if (body.Length == 0)
                {
                    continue;
                }

                if (TryParseBlockBegin(body, out string[]? beginRuleNames))
                {
                    foreach (string ruleName in beginRuleNames!)
                    {
                        if (!blockStarts.TryGetValue(ruleName, out Stack<Token>? stack))
                        {
                            stack = new Stack<Token>();
                            blockStarts[ruleName] = stack;
                        }

                        stack.Push(token);
                    }
                }
                else if (TryParseBlockEnd(body, out string[]? endRuleNames))
                {
                    foreach (string ruleName in endRuleNames!)
                    {
                        if (blockStarts.TryGetValue(ruleName, out Stack<Token>? stack)
                            && stack.Count > 0)
                        {
                            Token beginToken = stack.Pop();
                            AddSuppression(
                                suppressions,
                                ruleName,
                                beginToken.Extent.StartOffset,
                                token.Extent.EndOffset,
                                beginToken.Extent.StartLineNumber,
                                token.Extent.EndLineNumber);
                        }
                    }
                }
                else if (TryParseLineOrInline(body, out string[]? ruleNames))
                {
                    bool isInline = IsInlineComment(token, tokens, i);

                    if (isInline)
                    {
                        int line = token.Extent.StartLineNumber;
                        foreach (string ruleName in ruleNames!)
                        {
                            AddLineSuppression(suppressions, ruleName, line, line);
                        }
                    }
                    else
                    {
                        int nextCodeLine = FindNextNonCommentLine(tokens, i);
                        if (nextCodeLine > 0)
                        {
                            foreach (string ruleName in ruleNames!)
                            {
                                AddLineSuppression(suppressions, ruleName, nextCodeLine, nextCodeLine);
                            }
                        }
                    }
                }
            }
        }

        private static bool TryParseBlockBegin(string body, out string[]? ruleNames)
        {
            ruleNames = null;
            if (!body.StartsWith(BlockBeginPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string remainder = body.Substring(BlockBeginPrefix.Length).Trim();
            if (remainder.Length == 0)
            {
                return false;
            }

            ruleNames = ParseRuleNameList(remainder);
            return ruleNames.Length > 0;
        }

        private static bool TryParseBlockEnd(string body, out string[]? ruleNames)
        {
            ruleNames = null;
            if (!body.StartsWith(BlockEndPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string remainder = body.Substring(BlockEndPrefix.Length).Trim();
            if (remainder.Length == 0)
            {
                return false;
            }

            ruleNames = ParseRuleNameList(remainder);
            return ruleNames.Length > 0;
        }

        private static bool TryParseLineOrInline(string body, out string[]? ruleNames)
        {
            ruleNames = null;
            if (!body.StartsWith(PragmaPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (body.Length > PragmaPrefix.Length
                && body[PragmaPrefix.Length] == '-')
            {
                return false;
            }

            string remainder = body.Substring(PragmaPrefix.Length).Trim();
            if (remainder.Length == 0)
            {
                return false;
            }

            ruleNames = ParseRuleNameList(remainder);
            return ruleNames.Length > 0;
        }

        private static string[] ParseRuleNameList(string text)
        {
            string[] parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }

            return result.ToArray();
        }

        private static bool IsInlineComment(Token commentToken, Token[] tokens, int commentIndex)
        {
            int commentLine = commentToken.Extent.StartLineNumber;

            for (int j = commentIndex - 1; j >= 0; j--)
            {
                Token prev = tokens[j];
                if (prev.Kind == TokenKind.NewLine || prev.Kind == TokenKind.EndOfInput)
                {
                    break;
                }

                if (prev.Extent.StartLineNumber == commentLine && prev.Kind != TokenKind.Comment)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindNextNonCommentLine(Token[] tokens, int currentIndex)
        {
            for (int j = currentIndex + 1; j < tokens.Length; j++)
            {
                Token next = tokens[j];
                if (next.Kind == TokenKind.NewLine || next.Kind == TokenKind.EndOfInput)
                {
                    continue;
                }

                if (next.Kind != TokenKind.Comment)
                {
                    return next.Extent.StartLineNumber;
                }
            }

            return -1;
        }

        private static void AddSuppression(
            Dictionary<string, List<RuleSuppression>> suppressions,
            string ruleName,
            int startOffset,
            int endOffset,
            int startLineNumber,
            int endLineNumber)
        {
            var suppression = new RuleSuppression(
                ruleName,
                ruleSuppressionId: null,
                startOffset,
                endOffset,
                startLineNumber,
                endLineNumber,
                justification: null);

            if (!suppressions.TryGetValue(ruleName, out List<RuleSuppression>? list))
            {
                list = new List<RuleSuppression>();
                suppressions[ruleName] = list;
            }

            list.Add(suppression);
        }

        private static void AddLineSuppression(
            Dictionary<string, List<RuleSuppression>> suppressions,
            string ruleName,
            int startLine,
            int endLine)
        {
            var suppression = new RuleSuppression(
                ruleName,
                ruleSuppressionId: null,
                startOffset: 0,
                endOffset: int.MaxValue,
                startLineNumber: startLine,
                endLineNumber: endLine,
                justification: null);

            if (!suppressions.TryGetValue(ruleName, out List<RuleSuppression>? list))
            {
                list = new List<RuleSuppression>();
                suppressions[ruleName] = list;
            }

            list.Add(suppression);
        }
    }
}
