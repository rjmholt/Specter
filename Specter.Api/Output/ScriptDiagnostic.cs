using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.CommandDatabase;

namespace Specter
{
    public class ScriptDiagnostic
    {
        public ScriptDiagnostic(
            RuleInfo? rule,
            string message,
            IScriptExtent scriptExtent,
            DiagnosticSeverity severity)
            : this(rule, message, scriptExtent, severity, corrections: null)
        {
        }

        public ScriptDiagnostic(
            RuleInfo? rule,
            string message,
            IScriptExtent scriptExtent,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections)
            : this(
                rule,
                message,
                scriptExtent,
                severity,
                corrections,
                ruleSuppressionId: null,
                command: null,
                parameter: null,
                targetPlatform: null)
        {
        }

        public ScriptDiagnostic(
            RuleInfo? rule,
            string message,
            IScriptExtent scriptExtent,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
        {
            Rule = rule;
            Corrections = CopyCorrections(corrections);
            Message = message;
            ScriptExtent = scriptExtent;
            Severity = severity;
            RuleSuppressionId = ruleSuppressionId;
            Command = command;
            Parameter = parameter;
            TargetPlatform = targetPlatform;
        }

        public RuleInfo? Rule { get; }

        public string Message { get; }

        public IScriptExtent ScriptExtent { get; }

        public DiagnosticSeverity Severity { get; }

        public IReadOnlyList<Correction>? Corrections { get; }

        public string? RuleSuppressionId { get; }

        public string? Command { get; }

        public string? Parameter { get; }

        public PlatformInfo? TargetPlatform { get; }

        public string DisplayHeader
            => string.Format(
                CultureInfo.CurrentCulture,
                "{0}[{1}]: {2}",
                Severity.ToString().ToLowerInvariant(),
                Rule?.FullName ?? "UnknownRule",
                Message);

        public string DisplayLocation
            => string.Format(
                CultureInfo.CurrentCulture,
                "  --> {0}:{1}:{2}",
                GetScriptName(ScriptExtent),
                ScriptExtent?.StartLineNumber ?? 0,
                ScriptExtent?.StartColumnNumber ?? 0);

        public string DisplayGutter => "   |";

        public string DisplaySourceLine
        {
            get
            {
                if (ScriptExtent is null
                    || ScriptExtent.StartLineNumber <= 0
                    || string.IsNullOrEmpty(ScriptExtent.Text))
                {
                    return string.Empty;
                }

                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} | {1}",
                    ScriptExtent.StartLineNumber,
                    ScriptExtent.Text);
            }
        }

        public string DisplayUnderline
        {
            get
            {
                if (ScriptExtent is null
                    || ScriptExtent.StartLineNumber <= 0
                    || string.IsNullOrEmpty(ScriptExtent.Text))
                {
                    return string.Empty;
                }

                int startColumn = Math.Max(1, ScriptExtent.StartColumnNumber);
                int endColumn = Math.Max(startColumn + 1, ScriptExtent.EndColumnNumber);
                int lineWidth = ScriptExtent.StartLineNumber.ToString(CultureInfo.CurrentCulture).Length;
                int prefixWidth = lineWidth + 3 + (startColumn - 1);

                return new string(' ', prefixWidth) + new string('~', Math.Max(1, endColumn - startColumn));
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}:{1}:{2}: {3} {4} - {5}",
                GetScriptName(ScriptExtent),
                ScriptExtent?.StartLineNumber ?? 0,
                ScriptExtent?.StartColumnNumber ?? 0,
                Severity,
                Rule?.FullName ?? "UnknownRule",
                Message);
        }

        private static string GetScriptName(IScriptExtent? extent)
        {
            if (extent is null || string.IsNullOrEmpty(extent.File))
            {
                return "Script";
            }

            return System.IO.Path.GetFileName(extent.File);
        }

        private static IReadOnlyList<Correction>? CopyCorrections(IReadOnlyList<Correction>? corrections)
        {
            if (corrections is null)
            {
                return null;
            }

            if (corrections.Count == 0)
            {
                return Array.Empty<Correction>();
            }

            var copy = new Correction[corrections.Count];
            for (int i = 0; i < corrections.Count; i++)
            {
                copy[i] = corrections[i];
            }

            return copy;
        }

    }

    public class ScriptAstDiagnostic : ScriptDiagnostic
    {
        public ScriptAstDiagnostic(RuleInfo? rule, string message, Ast ast, DiagnosticSeverity severity)
            : this(rule, message, ast, severity, corrections: null)
        {
        }

        public ScriptAstDiagnostic(
            RuleInfo? rule,
            string message,
            Ast ast,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections)
            : this(rule, message, ast, severity, corrections, ruleSuppressionId: null, command: null, parameter: null, targetPlatform: null)
        {
        }

        public ScriptAstDiagnostic(
            RuleInfo? rule,
            string message,
            Ast ast,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
            : base(rule, message, ast.Extent, severity, corrections, ruleSuppressionId, command, parameter, targetPlatform)
        {
            Ast = ast;
        }

        public Ast Ast { get; }
    }

    public class ScriptTokenDiagnostic : ScriptDiagnostic
    {
        public ScriptTokenDiagnostic(RuleInfo? rule, string message, Token token, DiagnosticSeverity severity)
            : this(rule, message, token, severity, corrections: null)
        {
        }

        public ScriptTokenDiagnostic(RuleInfo? rule, string message, Token token, DiagnosticSeverity severity, IReadOnlyList<Correction>? corrections)
            : this(rule, message, token, severity, corrections, ruleSuppressionId: null, command: null, parameter: null, targetPlatform: null)
        {
        }

        public ScriptTokenDiagnostic(
            RuleInfo? rule,
            string message,
            Token token,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
            : base(rule, message, token.Extent, severity, corrections, ruleSuppressionId, command, parameter, targetPlatform)
        {
            Token = token;
        }

        public Token Token { get; }
    }
}
