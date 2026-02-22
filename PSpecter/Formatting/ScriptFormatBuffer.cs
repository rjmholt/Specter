using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text;

namespace PSpecter.Formatting
{
    /// <summary>
    /// Mutable buffer holding the current script content, AST, and tokens.
    /// Edits are applied via StringBuilder and the buffer is reparsed to keep
    /// the AST/token snapshot consistent.
    /// </summary>
    internal sealed class ScriptFormatBuffer
    {
        private string _content;
        private Ast _ast;
        private IReadOnlyList<Token> _tokens;
        private readonly string? _filePath;

        private ScriptFormatBuffer(string content, Ast ast, IReadOnlyList<Token> tokens, string? filePath)
        {
            _content = content;
            _ast = ast;
            _tokens = tokens;
            _filePath = filePath;
        }

        public string Content => _content;

        public Ast Ast => _ast;

        public IReadOnlyList<Token> Tokens => _tokens;

        public static ScriptFormatBuffer FromScript(string scriptContent, string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            Ast ast = Parser.ParseInput(scriptContent, out Token[] tokens, out _);
            return new ScriptFormatBuffer(scriptContent, ast, tokens, filePath);
        }

        /// <summary>
        /// Apply a set of non-overlapping edits and reparse.
        /// Returns true if any edits were applied (and a reparse occurred).
        /// </summary>
        public bool ApplyEdits(IReadOnlyList<ScriptEdit> edits)
        {
            if (edits is null || edits.Count == 0)
            {
                return false;
            }

            var sorted = new List<ScriptEdit>(edits);
            sorted.Sort();

            ValidateNoOverlaps(sorted);

            var sb = new StringBuilder(_content);

            foreach (ScriptEdit edit in sorted)
            {
                sb.Remove(edit.StartOffset, edit.OriginalLength);
                sb.Insert(edit.StartOffset, edit.NewText);
            }

            _content = sb.ToString();
            _ast = Parser.ParseInput(_content, out Token[] tokens, out _);
            _tokens = tokens;
            return true;
        }

        private static void ValidateNoOverlaps(List<ScriptEdit> sortedDescending)
        {
            for (int i = 0; i < sortedDescending.Count - 1; i++)
            {
                ScriptEdit current = sortedDescending[i];
                ScriptEdit next = sortedDescending[i + 1];

                if (next.EndOffset > current.StartOffset)
                {
                    throw new InvalidOperationException(
                        $"Overlapping edits detected: {next} overlaps with {current}");
                }
            }
        }

        public override string ToString() => _content;
    }
}
