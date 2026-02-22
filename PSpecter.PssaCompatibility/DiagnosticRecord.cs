#nullable disable

using System.Collections.Generic;
using System.Management.Automation.Language;
using EngineDiagnostic = PSpecter.ScriptDiagnostic;
using EngineSeverity = PSpecter.DiagnosticSeverity;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    /// <summary>
    /// Compatibility adapter that wraps a ScriptAnalyzer2 ScriptDiagnostic
    /// to present the original PSScriptAnalyzer DiagnosticRecord surface.
    /// </summary>
    public class DiagnosticRecord
    {
        public string Message { get; }

        public IScriptExtent Extent { get; }

        public string RuleName { get; }

        public DiagnosticSeverity Severity { get; }

        public string ScriptName
        {
            get
            {
                return string.IsNullOrEmpty(ScriptPath)
                    ? string.Empty
                    : System.IO.Path.GetFileName(ScriptPath);
            }
        }

        public string ScriptPath { get; }

        public string RuleSuppressionID { get; set; }

        public IReadOnlyList<CorrectionExtent> SuggestedCorrections { get; }

        public bool IsSuppressed { get; }

        public DiagnosticRecord(
            string message,
            IScriptExtent extent,
            string ruleName,
            DiagnosticSeverity severity,
            string scriptPath,
            string ruleId = null,
            IReadOnlyList<CorrectionExtent> suggestedCorrections = null)
        {
            Message = message ?? string.Empty;
            Extent = extent;
            RuleName = ruleName ?? string.Empty;
            Severity = severity;
            ScriptPath = scriptPath ?? string.Empty;
            RuleSuppressionID = ruleId;
            SuggestedCorrections = suggestedCorrections;
        }

        public override string ToString() => Message;

        internal static DiagnosticRecord FromEngineDiagnostic(EngineDiagnostic diagnostic)
        {
            string ruleName = MapRuleName(diagnostic);
            DiagnosticSeverity severity = MapSeverity(diagnostic.Severity);
            string scriptPath = diagnostic.ScriptExtent?.File;

            IReadOnlyList<CorrectionExtent> corrections = null;
            if (diagnostic.Corrections is { Count: > 0 })
            {
                var list = new List<CorrectionExtent>(diagnostic.Corrections.Count);
                foreach (var correction in diagnostic.Corrections)
                {
                    list.Add(CorrectionExtent.FromEngineCorrection(correction, scriptPath));
                }
                corrections = list;
            }

            return new DiagnosticRecord(
                diagnostic.Message,
                diagnostic.ScriptExtent,
                ruleName,
                severity,
                scriptPath,
                ruleId: diagnostic.RuleSuppressionId,
                suggestedCorrections: corrections);
        }

        /// <summary>
        /// Maps a ScriptAnalyzer2 rule name to the original PSSA format.
        /// Engine uses namespace "PS" and name "AvoidFoo" -> PSSA uses "PSAvoidFoo".
        /// </summary>
        private static string MapRuleName(EngineDiagnostic diagnostic)
        {
            if (diagnostic.Rule is null)
            {
                return string.Empty;
            }

            return diagnostic.Rule.Namespace + diagnostic.Rule.Name;
        }

        private static DiagnosticSeverity MapSeverity(EngineSeverity engineSeverity)
        {
            return PSpecter.PssaCompatibility.SeverityMapper.ToCompat(engineSeverity);
        }
    }
}
