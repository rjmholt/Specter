using PSpecter.Configuration;
using PSpecter.Formatting;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter.Rules
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

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent, IReadOnlyList<Correction> corrections)
            => CreateDiagnostic(message, extent, RuleInfo.DefaultSeverity, corrections);

        protected ScriptDiagnostic CreateDiagnostic(string message, IScriptExtent extent, DiagnosticSeverity severity, IReadOnlyList<Correction> corrections)
        {
            return new ScriptDiagnostic(RuleInfo, message, extent, severity, corrections);
        }
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

        public abstract IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string scriptPath);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast)
            => CreateDiagnostic(message, ast, RuleInfo.DefaultSeverity);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, DiagnosticSeverity severity)
            => CreateDiagnostic(message, ast, severity, corrections: null);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, IReadOnlyList<Correction> corrections)
            => CreateDiagnostic(message, ast, RuleInfo.DefaultSeverity, corrections);

        protected ScriptAstDiagnostic CreateDiagnostic(string message, Ast ast, DiagnosticSeverity severity, IReadOnlyList<Correction> corrections)
        {
            return new ScriptAstDiagnostic(RuleInfo, message, ast, severity, corrections);
        }

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token)
            => CreateDiagnostic(message, token, RuleInfo.DefaultSeverity);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, DiagnosticSeverity severity)
            => CreateDiagnostic(message, token, severity, corrections: null);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, IReadOnlyList<Correction> corrections)
            => CreateDiagnostic(message, token, RuleInfo.DefaultSeverity, corrections);

        protected ScriptTokenDiagnostic CreateDiagnostic(string message, Token token, DiagnosticSeverity severity, IReadOnlyList<Correction> corrections)
        {
            return new ScriptTokenDiagnostic(RuleInfo, message, token, severity, corrections);
        }
    }

    public abstract class ConfigurableScriptRule<TConfiguration> : ScriptRule, IConfigurableRule<TConfiguration> where TConfiguration : IRuleConfiguration
    {
        protected ConfigurableScriptRule(RuleInfo ruleInfo, TConfiguration ruleConfiguration) : base(ruleInfo)
        {
            Configuration = ruleConfiguration;
        }

        public TConfiguration Configuration { get; }
    }
}
