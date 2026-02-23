using Specter.Logging;
using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Specter.Execution
{
    public interface IRuleExecutor
    {
        void AddRule(ScriptRule rule);

        IReadOnlyCollection<ScriptDiagnostic> CollectDiagnostics();

        IReadOnlyList<RuleExecutionError> Errors { get; }
    }

    internal class SequentialRuleExecutor : IRuleExecutor
    {
        private readonly Ast _scriptAst;
        private readonly IReadOnlyList<Token> _scriptTokens;
        private readonly string? _scriptPath;
        private readonly IAnalysisLogger _logger;
        private readonly List<ScriptDiagnostic> _diagnostics;
        private readonly List<RuleExecutionError> _errors;

        public SequentialRuleExecutor(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath, IAnalysisLogger logger)
        {
            _scriptAst = ast;
            _scriptTokens = tokens;
            _scriptPath = scriptPath;
            _logger = logger;
            _diagnostics = new List<ScriptDiagnostic>();
            _errors = new List<RuleExecutionError>();
        }

        public IReadOnlyList<RuleExecutionError> Errors => _errors;

        public void AddRule(ScriptRule rule)
        {
            try
            {
                _diagnostics.AddRange(rule.AnalyzeScript(_scriptAst, _scriptTokens, _scriptPath));
            }
            catch (Exception ex)
            {
                var error = new RuleExecutionError(rule.RuleInfo.Name, ex);
                _errors.Add(error);
                _logger.Warning($"Rule '{rule.RuleInfo.Name}' failed during execution: {ex.Message}");
            }
        }

        public IReadOnlyCollection<ScriptDiagnostic> CollectDiagnostics()
        {
            return _diagnostics;
        }
    }

    internal class ParallelLinqRuleExecutor : IRuleExecutor
    {
        private readonly Ast _scriptAst;
        private readonly IReadOnlyList<Token> _scriptTokens;
        private readonly string? _scriptPath;
        private readonly IAnalysisLogger _logger;
        private readonly List<ScriptRule> _parallelRules;
        private readonly List<ScriptRule> _sequentialRules;
        private readonly List<RuleExecutionError> _errors;

        public ParallelLinqRuleExecutor(Ast scriptAst, IReadOnlyList<Token> scriptTokens, string? scriptPath, IAnalysisLogger logger)
        {
            _scriptAst = scriptAst;
            _scriptTokens = scriptTokens;
            _scriptPath = scriptPath;
            _logger = logger;
            _parallelRules = new List<ScriptRule>();
            _sequentialRules = new List<ScriptRule>();
            _errors = new List<RuleExecutionError>();
        }

        public IReadOnlyList<RuleExecutionError> Errors => _errors;

        public void AddRule(ScriptRule rule)
        {
            if (rule.RuleInfo.IsThreadsafe)
            {
                _parallelRules.Add(rule);
                return;
            }

            _sequentialRules.Add(rule);
        }

        public IReadOnlyCollection<ScriptDiagnostic> CollectDiagnostics()
        {
            var parallelErrors = new List<RuleExecutionError>();
            List<ScriptDiagnostic> diagnostics = _parallelRules.AsParallel()
                .SelectMany(rule => ExecuteRuleSafe(rule, parallelErrors))
                .ToList();

            lock (_errors)
            {
                _errors.AddRange(parallelErrors);
            }

            foreach (ScriptRule sequentialRule in _sequentialRules)
            {
                try
                {
                    diagnostics.AddRange(sequentialRule.AnalyzeScript(_scriptAst, _scriptTokens, _scriptPath));
                }
                catch (Exception ex)
                {
                    var error = new RuleExecutionError(sequentialRule.RuleInfo.Name, ex);
                    _errors.Add(error);
                    _logger.Warning($"Rule '{sequentialRule.RuleInfo.Name}' failed during execution: {ex.Message}");
                }
            }

            return diagnostics;
        }

        private IEnumerable<ScriptDiagnostic> ExecuteRuleSafe(ScriptRule rule, List<RuleExecutionError> errors)
        {
            try
            {
                return rule.AnalyzeScript(_scriptAst, _scriptTokens, _scriptPath);
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(new RuleExecutionError(rule.RuleInfo.Name, ex));
                }
                _logger.Warning($"Rule '{rule.RuleInfo.Name}' failed during execution: {ex.Message}");
                return Array.Empty<ScriptDiagnostic>();
            }
        }
    }
}
