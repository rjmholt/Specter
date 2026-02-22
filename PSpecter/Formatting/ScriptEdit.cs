using System;

namespace PSpecter.Formatting
{
    /// <summary>
    /// A single text replacement identified by character offsets into the original script content.
    /// Editors produce these; the format buffer consumes them.
    /// </summary>
    public readonly struct ScriptEdit : IComparable<ScriptEdit>
    {
        public ScriptEdit(int startOffset, int endOffset, string newText)
        {
            if (startOffset < 0) { throw new ArgumentOutOfRangeException(nameof(startOffset)); }
            if (endOffset < startOffset) { throw new ArgumentOutOfRangeException(nameof(endOffset)); }

            StartOffset = startOffset;
            EndOffset = endOffset;
            NewText = newText ?? string.Empty;
        }

        /// <summary>Inclusive start offset in the original script content.</summary>
        public int StartOffset { get; }

        /// <summary>Exclusive end offset in the original script content.</summary>
        public int EndOffset { get; }

        /// <summary>The replacement text. Empty string means deletion.</summary>
        public string NewText { get; }

        /// <summary>Number of characters in the original content that this edit replaces.</summary>
        public int OriginalLength => EndOffset - StartOffset;

        /// <summary>
        /// Orders edits by descending start offset so they can be applied back-to-front
        /// without invalidating earlier offsets.
        /// </summary>
        public int CompareTo(ScriptEdit other)
        {
            return other.StartOffset.CompareTo(StartOffset);
        }

        public override string ToString()
            => $"[{StartOffset}..{EndOffset}) -> \"{NewText}\"";
    }
}
