using Specter.Logging;
using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter.Execution
{
    public interface IRuleExecutorFactory
    {
        IRuleExecutor CreateRuleExecutor(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath);
    }

    public class SequentialRuleExecutorFactory : IRuleExecutorFactory
    {
        private readonly IAnalysisLogger _logger;

        public SequentialRuleExecutorFactory(IAnalysisLogger? logger = null)
        {
            _logger = logger ?? NullAnalysisLogger.Instance;
        }

        public IRuleExecutor CreateRuleExecutor(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            return new SequentialRuleExecutor(ast, tokens, scriptPath, _logger);
        }
    }

    public class ParallelRuleExecutorFactory : IRuleExecutorFactory
    {
        private readonly IAnalysisLogger _logger;
        private readonly int _maxDegreeOfParallelism;

        public ParallelRuleExecutorFactory(IAnalysisLogger? logger = null, int? maxDegreeOfParallelism = null)
        {
            _logger = logger ?? NullAnalysisLogger.Instance;
            int configuredParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
            _maxDegreeOfParallelism = configuredParallelism > 0 ? configuredParallelism : Environment.ProcessorCount;
        }

        public IRuleExecutor CreateRuleExecutor(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            return new ParallelRuleExecutor(ast, tokens, scriptPath, _logger, _maxDegreeOfParallelism);
        }
    }
}
