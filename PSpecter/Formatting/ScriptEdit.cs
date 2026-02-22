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
            : this(startOffset, endOffset, newText, diagnosticStartOffset: -1, diagnosticEndOffset: -1)
        {
        }

        public ScriptEdit(int startOffset, int endOffset, string newText, int diagnosticStartOffset, int diagnosticEndOffset)
        {
            if (startOffset < 0) { throw new ArgumentOutOfRangeException(nameof(startOffset)); }
            if (endOffset < startOffset) { throw new ArgumentOutOfRangeException(nameof(endOffset)); }

            StartOffset = startOffset;
            EndOffset = endOffset;
            NewText = newText ?? string.Empty;
            DiagnosticStartOffset = diagnosticStartOffset;
            DiagnosticEndOffset = diagnosticEndOffset;
        }

        /// <summary>Inclusive start offset in the original script content.</summary>
        public int StartOffset { get; }

        /// <summary>Exclusive end offset in the original script content.</summary>
        public int EndOffset { get; }

        /// <summary>The replacement text. Empty string means deletion.</summary>
        public string NewText { get; }

        /// <summary>
        /// Optional start offset for the diagnostic highlight extent.
        /// When -1, the edit's StartOffset is used.
        /// </summary>
        public int DiagnosticStartOffset { get; }

        /// <summary>
        /// Optional end offset for the diagnostic highlight extent.
        /// When -1, the edit's EndOffset is used.
        /// </summary>
        public int DiagnosticEndOffset { get; }

        public bool HasDiagnosticExtent => DiagnosticStartOffset >= 0 && DiagnosticEndOffset >= 0;

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
