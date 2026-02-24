using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Suppression;
using EngineDiagnostic = Specter.ScriptDiagnostic;
using EngineSeverity = Specter.DiagnosticSeverity;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    public class DiagnosticRecord
    {
        public string Message { get; }

        public IScriptExtent? Extent { get; }

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

        public string? RuleSuppressionID { get; }

        public IReadOnlyList<CorrectionExtent>? SuggestedCorrections { get; }

        public virtual bool IsSuppressed => false;

        /// <summary>
        /// For compatibility diagnostics: the command name flagged by the rule.
        /// Populated by UseCompatibleCommands.
        /// </summary>
        public string? Command { get; }

        /// <summary>
        /// For compatibility diagnostics: the parameter name flagged by the rule.
        /// Null for command-level diagnostics.
        /// </summary>
        public string? Parameter { get; }

        /// <summary>
        /// For compatibility diagnostics: the target platform that lacks the command or parameter.
        /// </summary>
        public CompatibilityTargetPlatform? TargetPlatform { get; }

        public DiagnosticRecord(
            string? message,
            IScriptExtent? extent,
            string? ruleName,
            DiagnosticSeverity severity,
            string? scriptPath,
            string? ruleId = null,
            IReadOnlyList<CorrectionExtent>? suggestedCorrections = null,
            string? command = null,
            string? parameter = null,
            CompatibilityTargetPlatform? targetPlatform = null)
        {
            Message = message ?? string.Empty;
            Extent = extent;
            RuleName = ruleName ?? string.Empty;
            Severity = severity;
            ScriptPath = scriptPath ?? string.Empty;
            RuleSuppressionID = ruleId;
            SuggestedCorrections = suggestedCorrections;
            Command = command;
            Parameter = parameter;
            TargetPlatform = targetPlatform;
        }

        public override string ToString() => Message;

        internal static DiagnosticRecord FromEngineDiagnostic(EngineDiagnostic diagnostic)
        {
            string ruleName = MapRuleName(diagnostic);
            DiagnosticSeverity severity = MapSeverity(diagnostic.Severity);
            string? scriptPath = diagnostic.ScriptExtent?.File;

            IReadOnlyList<CorrectionExtent>? corrections = MapCorrections(diagnostic, scriptPath);

            string? command = diagnostic.Command;
            string? parameter = diagnostic.Parameter;
            CompatibilityTargetPlatform? targetPlatform = diagnostic.TargetPlatform is null
                ? null
                : CompatibilityTargetPlatform.FromPlatformInfo(diagnostic.TargetPlatform);

            return new DiagnosticRecord(
                diagnostic.Message,
                diagnostic.ScriptExtent,
                ruleName,
                severity,
                scriptPath,
                ruleId: diagnostic.RuleSuppressionId,
                suggestedCorrections: corrections,
                command: command,
                parameter: parameter,
                targetPlatform: targetPlatform);
        }

        internal static SuppressedRecord FromSuppressedDiagnostic(SuppressedDiagnostic suppressed)
        {
            EngineDiagnostic diagnostic = suppressed.Diagnostic;
            string ruleName = MapRuleName(diagnostic);
            DiagnosticSeverity severity = MapSeverity(diagnostic.Severity);
            string? scriptPath = diagnostic.ScriptExtent?.File;

            IReadOnlyList<CorrectionExtent>? corrections = MapCorrections(diagnostic, scriptPath);

            var compatSuppressions = new List<CompatRuleSuppression>(suppressed.Suppressions.Count);
            foreach (RuleSuppression rs in suppressed.Suppressions)
            {
                compatSuppressions.Add(new CompatRuleSuppression(
                    rs.RuleName,
                    rs.RuleSuppressionId,
                    rs.Justification));
            }

            return new SuppressedRecord(
                diagnostic.Message,
                diagnostic.ScriptExtent,
                ruleName,
                severity,
                scriptPath,
                diagnostic.RuleSuppressionId,
                corrections,
                compatSuppressions);
        }

        internal static string MapRuleName(EngineDiagnostic diagnostic)
        {
            if (diagnostic.Rule is null)
            {
                return string.Empty;
            }

            return diagnostic.Rule.Namespace + diagnostic.Rule.Name;
        }

        private static IReadOnlyList<CorrectionExtent>? MapCorrections(EngineDiagnostic diagnostic, string? scriptPath)
        {
            if (diagnostic.Corrections is not { Count: > 0 })
            {
                return null;
            }

            var list = new List<CorrectionExtent>(diagnostic.Corrections.Count);
            foreach (var correction in diagnostic.Corrections)
            {
                list.Add(CorrectionExtent.FromEngineCorrection(correction, scriptPath));
            }

            return list;
        }

        private static DiagnosticSeverity MapSeverity(EngineSeverity engineSeverity)
        {
            return Specter.PssaCompatibility.SeverityMapper.ToCompat(engineSeverity);
        }
    }

    public class SuppressedRecord : DiagnosticRecord
    {
        public SuppressedRecord(
            string? message,
            IScriptExtent? extent,
            string? ruleName,
            DiagnosticSeverity severity,
            string? scriptPath,
            string? ruleId,
            IReadOnlyList<CorrectionExtent>? suggestedCorrections,
            IReadOnlyList<CompatRuleSuppression> suppression)
            : base(message, extent, ruleName, severity, scriptPath, ruleId, suggestedCorrections)
        {
            Suppression = suppression;
        }

        public override bool IsSuppressed => true;

        public IReadOnlyList<CompatRuleSuppression> Suppression { get; }
    }

    public class CompatRuleSuppression
    {
        public CompatRuleSuppression(
            string ruleName,
            string? ruleSuppressionId,
            string? justification)
        {
            RuleName = ruleName;
            RuleSuppressionID = ruleSuppressionId;
            Justification = justification;
        }

        public string RuleName { get; }

        public string? RuleSuppressionID { get; }

        public string? Justification { get; }
    }
}
