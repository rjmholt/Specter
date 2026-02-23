using Specter.Logging;
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

    public class ParallelLinqRuleExecutorFactory : IRuleExecutorFactory
    {
        private readonly IAnalysisLogger _logger;

        public ParallelLinqRuleExecutorFactory(IAnalysisLogger? logger = null)
        {
            _logger = logger ?? NullAnalysisLogger.Instance;
        }

        public IRuleExecutor CreateRuleExecutor(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            return new ParallelLinqRuleExecutor(ast, tokens, scriptPath, _logger);
        }
    }
}
