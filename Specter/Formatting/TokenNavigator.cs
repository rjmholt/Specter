using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter.Formatting
{
    /// <summary>
    /// Provides efficient, comment-aware navigation over a token stream.
    /// Editors should use this to detect comments in edit regions before
    /// producing edits that could displace them.
    /// </summary>
    internal sealed class TokenNavigator
    {
        private readonly IReadOnlyList<Token> _tokens;

        internal TokenNavigator(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        internal IReadOnlyList<Token> Tokens => _tokens;

        internal int Count => _tokens.Count;

        internal Token this[int index] => _tokens[index];

        /// <summary>
        /// Returns true if any comment token overlaps the given offset range.
        /// </summary>
        internal bool HasCommentInRange(int startOffset, int endOffset)
        {
            for (int i = 0; i < _tokens.Count; i++)
            {
                Token t = _tokens[i];

                if (t.Extent.StartOffset >= endOffset)
                {
                    break;
                }

                if (t.Extent.EndOffset <= startOffset)
                {
                    continue;
                }

                if (t.Kind == TokenKind.Comment)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns all tokens whose extents overlap the given offset range.
        /// </summary>
        internal List<Token> GetTokensInRange(int startOffset, int endOffset)
        {
            var result = new List<Token>();

            for (int i = 0; i < _tokens.Count; i++)
            {
                Token t = _tokens[i];

                if (t.Extent.StartOffset >= endOffset)
                {
                    break;
                }

                if (t.Extent.EndOffset <= startOffset)
                {
                    continue;
                }

                result.Add(t);
            }

            return result;
        }

        /// <summary>
        /// Returns the index of the next token after <paramref name="index"/> that
        /// is not a newline or whitespace-only token, or -1 if none exists.
        /// </summary>
        internal int NextNonWhitespaceToken(int index)
        {
            for (int i = index + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Kind != TokenKind.NewLine
                    && _tokens[i].Kind != TokenKind.EndOfInput)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the previous token before <paramref name="index"/> that
        /// is not a newline or whitespace-only token, or -1 if none exists.
        /// </summary>
        internal int PreviousNonWhitespaceToken(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (_tokens[i].Kind != TokenKind.NewLine
                    && _tokens[i].Kind != TokenKind.EndOfInput)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the next token after <paramref name="index"/> that
        /// is not a comment, or -1 if none exists.
        /// </summary>
        internal int NextTokenIgnoringComments(int index)
        {
            for (int i = index + 1; i < _tokens.Count; i++)
            {
                if (_tokens[i].Kind != TokenKind.Comment)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the previous token before <paramref name="index"/> that
        /// is not a comment, or -1 if none exists.
        /// </summary>
        internal int PreviousTokenIgnoringComments(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (_tokens[i].Kind != TokenKind.Comment)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the next token after <paramref name="index"/> that is
        /// not a newline, end-of-input, or comment, or -1 if none exists.
        /// </summary>
        internal int NextSignificantToken(int index)
        {
            for (int i = index + 1; i < _tokens.Count; i++)
            {
                TokenKind kind = _tokens[i].Kind;
                if (kind != TokenKind.NewLine
                    && kind != TokenKind.EndOfInput
                    && kind != TokenKind.Comment)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the previous token before <paramref name="index"/> that is
        /// not a newline, end-of-input, or comment, or -1 if none exists.
        /// </summary>
        internal int PreviousSignificantToken(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                TokenKind kind = _tokens[i].Kind;
                if (kind != TokenKind.NewLine
                    && kind != TokenKind.EndOfInput
                    && kind != TokenKind.Comment)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Find the token index for a token at or containing the given offset.
        /// Uses linear scan; for frequent lookups, callers should cache results.
        /// Returns -1 if no token contains the offset.
        /// </summary>
        internal int FindTokenAtOffset(int offset)
        {
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Extent.StartOffset <= offset && offset < _tokens[i].Extent.EndOffset)
                {
                    return i;
                }

                if (_tokens[i].Extent.StartOffset > offset)
                {
                    break;
                }
            }

            return -1;
        }
    }
}
