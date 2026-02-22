using System.Management.Automation.Language;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    /// <summary>
    /// Compatibility adapter that wraps a ScriptAnalyzer2 Correction
    /// to present the original PSScriptAnalyzer CorrectionExtent surface.
    /// </summary>
    public class CorrectionExtent
    {
        public CorrectionExtent(
            int startLineNumber,
            int endLineNumber,
            int startColumnNumber,
            int endColumnNumber,
            string text,
            string file,
            string description)
        {
            StartLineNumber = startLineNumber;
            EndLineNumber = endLineNumber;
            StartColumnNumber = startColumnNumber;
            EndColumnNumber = endColumnNumber;
            Text = text;
            File = file;
            Description = description;
        }

        public int StartLineNumber { get; }

        public int EndLineNumber { get; }

        public int StartColumnNumber { get; }

        public int EndColumnNumber { get; }

        public string Text { get; }

        public string File { get; }

        public string Description { get; }

        public override string ToString() => Text;

        internal static CorrectionExtent FromEngineCorrection(
            PSpecter.Correction correction,
            string filePath)
        {
            IScriptExtent extent = correction.Extent;
            return new CorrectionExtent(
                extent.StartLineNumber,
                extent.EndLineNumber,
                extent.StartColumnNumber,
                extent.EndColumnNumber,
                correction.CorrectionText,
                filePath,
                correction.Description);
        }
    }
}
