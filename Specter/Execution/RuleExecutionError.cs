using System;

namespace Specter.Execution
{
    public sealed class RuleExecutionError
    {
        public RuleExecutionError(string ruleName, Exception exception)
        {
            RuleName = ruleName;
            Exception = exception;
        }

        public string RuleName { get; }

        public Exception Exception { get; }

        public override string ToString() => $"Rule '{RuleName}' failed: {Exception.Message}";
    }
}
