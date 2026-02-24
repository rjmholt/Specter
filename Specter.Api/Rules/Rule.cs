using Specter.Configuration;
using Specter.CommandDatabase;
using Specter.Formatting;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter.Rules
{
    public interface IResettable
    {
        void Reset();
    }

    /// <summary>
    /// Implemented by rules that provide a formatting editor.
    /// The formatter can discover editors from rules via this interface.
    /// </summary>
    public interface IFormattingRule
    {
        IScriptEditor CreateEditor();
    }

    public abstract class Rule
    {
        protected Rule(RuleInfo ruleInfo)
        {
            RuleInfo = ruleInfo;
        }

        public RuleInfo RuleInfo { get; }

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent)
            => CreateDiagnostic(message, extent, RuleInfo.DefaultSeverity);

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent, DiagnosticSeverity severity)
            => CreateDiagnostic(message, extent, severity, corrections: null);

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent, IReadOnlyList<Correction>? corrections)
            => CreateDiagnostic(message, extent, RuleInfo.DefaultSeverity, corrections);

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent, DiagnosticSeverity severity, IReadOnlyList<Correction>? corrections)
            => CreateDiagnostic(message, extent, severity, corrections, ruleSuppressionId: null);

        protected ScriptDiagnostic CreateDiagnostic(
            string message,
            IScriptExtent extent,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
        {
            return CreateScriptDiagnosticCore(
                RuleInfo,
                message,
                extent,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);
        }

        /// <summary>
        /// Extension point for specialized diagnostics in derived rule bases.
        /// </summary>
        protected virtual ScriptDiagnostic CreateScriptDiagnosticCore(
            RuleInfo? ruleInfo,
            string message,
            IScriptExtent extent,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command,
            string? parameter,
            PlatformInfo? targetPlatform)
            => new ScriptDiagnostic(
                ruleInfo,
                message,
                extent,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);
    }

    public interface IConfigurableRule<TConfiguration> where TConfiguration : IRuleConfiguration
    {
        public TConfiguration Configuration { get; }
    }

    public abstract class ScriptRule : Rule
    {
        protected ScriptRule(RuleInfo ruleInfo) : base(ruleInfo)
        {
        }

        public virtual CommonConfiguration? CommonConfiguration => null;

        public abstract IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast)
            => CreateDiagnostic(message, ast, RuleInfo.DefaultSeverity);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, DiagnosticSeverity severity)
            => CreateDiagnostic(message, ast, severity, corrections: null);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, IReadOnlyList<Correction>? corrections)
            => CreateDiagnostic(message, ast, RuleInfo.DefaultSeverity, corrections);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, DiagnosticSeverity severity, IReadOnlyList<Correction>? corrections)
        {
            return CreateDiagnostic(message, ast, severity, corrections, ruleSuppressionId: null);
        }

        protected ScriptAstDiagnostic CreateDiagnostic(
            string message,
            Ast ast,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
        {
            return CreateScriptAstDiagnosticCore(
                RuleInfo,
                message,
                ast,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);
        }

        /// <summary>
        /// Extension point for specialized AST diagnostics in derived rule bases.
        /// </summary>
        protected virtual ScriptAstDiagnostic CreateScriptAstDiagnosticCore(
            RuleInfo? ruleInfo,
            string message,
            Ast ast,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command,
            string? parameter,
            PlatformInfo? targetPlatform)
            => new ScriptAstDiagnostic(
                ruleInfo,
                message,
                ast,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token)
            => CreateDiagnostic(message, token, RuleInfo.DefaultSeverity);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, DiagnosticSeverity severity)
            => CreateDiagnostic(message, token, severity, corrections: null);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, IReadOnlyList<Correction>? corrections)
            => CreateDiagnostic(message, token, RuleInfo.DefaultSeverity, corrections);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, DiagnosticSeverity severity, IReadOnlyList<Correction>? corrections)
        {
            return CreateDiagnostic(message, token, severity, corrections, ruleSuppressionId: null);
        }

        protected ScriptTokenDiagnostic CreateDiagnostic(
            string message,
            Token token,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command = null,
            string? parameter = null,
            PlatformInfo? targetPlatform = null)
        {
            return CreateScriptTokenDiagnosticCore(
                RuleInfo,
                message,
                token,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);
        }

        /// <summary>
        /// Extension point for specialized token diagnostics in derived rule bases.
        /// </summary>
        protected virtual ScriptTokenDiagnostic CreateScriptTokenDiagnosticCore(
            RuleInfo? ruleInfo,
            string message,
            Token token,
            DiagnosticSeverity severity,
            IReadOnlyList<Correction>? corrections,
            string? ruleSuppressionId,
            string? command,
            string? parameter,
            PlatformInfo? targetPlatform)
            => new ScriptTokenDiagnostic(
                ruleInfo,
                message,
                token,
                severity,
                corrections,
                ruleSuppressionId,
                command,
                parameter,
                targetPlatform);
    }

    public abstract class ConfigurableScriptRule<TConfiguration> : ScriptRule, IConfigurableRule<TConfiguration> where TConfiguration : IRuleConfiguration
    {
        protected ConfigurableScriptRule(RuleInfo ruleInfo, TConfiguration ruleConfiguration) : base(ruleInfo)
        {
            Configuration = ruleConfiguration;
        }

        public override CommonConfiguration? CommonConfiguration => Configuration?.Common;

        public TConfiguration Configuration { get; }
    }
}
