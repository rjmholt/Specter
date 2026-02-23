using PSpecter.Rules;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter
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
        {
            Rule = rule;
            Corrections = corrections;
            Message = message;
            ScriptExtent = scriptExtent;
            Severity = severity;
        }

        public RuleInfo? Rule { get; }

        public string Message { get; }

        public IScriptExtent ScriptExtent { get; }

        public DiagnosticSeverity Severity { get; }

        public IReadOnlyList<Correction>? Corrections { get; }

        public string? RuleSuppressionId { get; set; }

        /// <summary>
        /// Rule-specific properties that enrich the diagnostic for consumers.
        /// Rules like UseCompatibleCommands use this to attach structured metadata
        /// (e.g. command name, parameter name, target platform) that compatibility
        /// layers can expose as first-class properties.
        /// </summary>
        public Dictionary<string, object>? Properties { get; set; }
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
            : base(rule, message, ast.Extent, severity, corrections)
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
            : base(rule, message, token.Extent, severity, corrections)
        {
            Token = token;
        }

        public Token Token { get; }
    }

}
