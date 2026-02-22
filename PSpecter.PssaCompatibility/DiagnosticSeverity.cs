#nullable disable

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    /// <summary>
    /// Severity levels matching the original PSScriptAnalyzer DiagnosticSeverity enum,
    /// including the original numeric values for backward compatibility.
    /// </summary>
    public enum DiagnosticSeverity : uint
    {
        Information = 0,
        Warning = 1,
        Error = 2,
        ParseError = 3,
    }
}
