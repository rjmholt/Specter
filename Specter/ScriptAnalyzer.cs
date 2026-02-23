using Specter.Builder;
using Specter.Configuration;
using Specter.Execution;
using Specter.Instantiation;
using Specter.Logging;
using Specter.Rules;
using Specter.Suppression;
using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter
{
    public class ScriptAnalyzer
    {
        public static ScriptAnalyzer Create(
            RuleComponentProvider ruleComponentProvider,
            IRuleExecutorFactory executorFactory,
            IReadOnlyList<IRuleProviderFactory> ruleProviderFactories,
            IAnalysisLogger? logger = null)
        {
            var resolvedLogger = logger ?? NullAnalysisLogger.Instance;
            var ruleProviders = new List<IRuleProvider>(ruleProviderFactories.Count);
            foreach (IRuleProviderFactory ruleProviderFactory in ruleProviderFactories)
            {
                ruleProviders.Add(ruleProviderFactory.CreateRuleProvider(ruleComponentProvider));
            }

            resolvedLogger.Debug($"Created analyzer with {ruleProviders.Count} rule providers");

            return new ScriptAnalyzer(executorFactory, ruleProviders, resolvedLogger);
        }

        private readonly IRuleExecutorFactory _executorFactory;
        private readonly IAnalysisLogger _logger;

        private ScriptAnalyzer(
            IRuleExecutorFactory executorFactory,
            IReadOnlyList<IRuleProvider> ruleProviders,
            IAnalysisLogger logger)
        {
            RuleProviders = ruleProviders;
            _executorFactory = executorFactory;
            _logger = logger;
        }

        public IReadOnlyList<IRuleProvider> RuleProviders { get; }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Script path must not be null or empty.", nameof(path));
            }

            _logger.Verbose($"Analyzing file: {path}");
            Ast ast = Parser.ParseFile(path, out Token[] tokens, out ParseError[] parseErrors);
            if (parseErrors.Length > 0)
            {
                _logger.Debug($"Parse errors in '{path}': {parseErrors.Length}");
            }
            return AnalyzeScript(ast, tokens, path);
        }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptInput(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            _logger.Debug("Analyzing script input");
            Ast ast = Parser.ParseInput(input, out Token[] tokens, out ParseError[] parseErrors);
            return AnalyzeScript(ast, tokens, scriptPath: null);
        }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScript(Ast scriptAst, Token[] scriptTokens) =>
            AnalyzeScript(scriptAst, scriptTokens, scriptPath: null);

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScript(Ast scriptAst, Token[] scriptTokens, string? scriptPath)
        {
            IRuleExecutor ruleExecutor = _executorFactory.CreateRuleExecutor(scriptAst, scriptTokens, scriptPath);
            int ruleCount = AddApplicableRules(ruleExecutor, scriptPath);
            _logger.Debug($"Executed {ruleCount} rules");

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = ruleExecutor.CollectDiagnostics();

            foreach (RuleExecutionError error in ruleExecutor.Errors)
            {
                _logger.Warning(error.ToString());
            }

            Dictionary<string, List<RuleSuppression>> suppressions = SuppressionParser.GetSuppressions(scriptAst, scriptTokens);
            if (suppressions.Count > 0)
            {
                _logger.Debug($"Found {suppressions.Count} suppression groups");
            }

            return SuppressionApplier.ApplySuppressions(diagnostics, suppressions);
        }

        public AnalysisResult AnalyzeScriptPathFull(string path)
        {
            _logger.Verbose($"Analyzing file (full): {path}");
            Ast ast = Parser.ParseFile(path, out Token[] tokens, out ParseError[] parseErrors);
            return AnalyzeScriptFull(ast, tokens, path);
        }

        public AnalysisResult AnalyzeScriptInputFull(string input)
        {
            _logger.Debug("Analyzing script input (full)");
            Ast ast = Parser.ParseInput(input, out Token[] tokens, out ParseError[] parseErrors);
            return AnalyzeScriptFull(ast, tokens, scriptPath: null);
        }

        public AnalysisResult AnalyzeScriptFull(Ast scriptAst, Token[] scriptTokens, string? scriptPath)
        {
            IRuleExecutor ruleExecutor = _executorFactory.CreateRuleExecutor(scriptAst, scriptTokens, scriptPath);
            int ruleCount = AddApplicableRules(ruleExecutor, scriptPath);
            _logger.Debug($"Executed {ruleCount} rules");

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = ruleExecutor.CollectDiagnostics();
            IReadOnlyList<RuleExecutionError> ruleErrors = ruleExecutor.Errors;

            foreach (RuleExecutionError error in ruleErrors)
            {
                _logger.Warning(error.ToString());
            }

            Dictionary<string, List<RuleSuppression>> suppressions = SuppressionParser.GetSuppressions(scriptAst, scriptTokens);
            return SuppressionApplier.ApplySuppressionsWithTracking(diagnostics, suppressions, ruleErrors);
        }

        private int AddApplicableRules(IRuleExecutor ruleExecutor, string? scriptPath)
        {
            int ruleCount = 0;
            foreach (IRuleProvider ruleProvider in RuleProviders)
            {
                foreach (ScriptRule rule in ruleProvider.GetScriptRules())
                {
                    CommonConfiguration? common = rule.CommonConfiguration;
                    if (common is not null && common.IsPathExcluded(scriptPath))
                    {
                        _logger.Debug($"Skipping rule '{rule.RuleInfo.Name}' for '{scriptPath}' (ExcludePaths)");
                        continue;
                    }

                    ruleExecutor.AddRule(rule);
                    ruleCount++;
                }
            }

            return ruleCount;
        }
    }
}
