using CompatSeverity = Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticSeverity;
using EngineSeverity = Microsoft.PowerShell.ScriptAnalyzer.DiagnosticSeverity;

namespace PSLint.PssaCompatibility
{
    internal static class SeverityMapper
    {
        public static CompatSeverity ToCompat(EngineSeverity engineSeverity)
        {
            return engineSeverity switch
            {
                EngineSeverity.Information => CompatSeverity.Information,
                EngineSeverity.Warning => CompatSeverity.Warning,
                EngineSeverity.Error => CompatSeverity.Error,
                EngineSeverity.ParseError => CompatSeverity.ParseError,
                _ => CompatSeverity.Warning,
            };
        }
    }
}
