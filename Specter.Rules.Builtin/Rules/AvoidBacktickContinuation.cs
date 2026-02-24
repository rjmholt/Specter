using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidBacktickContinuation", typeof(Strings), nameof(Strings.AvoidBacktickContinuationDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidBacktickContinuation : ScriptRule
    {
        internal AvoidBacktickContinuation(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind == TokenKind.LineContinuation)
                {
                    yield return CreateDiagnostic(
                        Strings.AvoidBacktickContinuationError,
                        tokens[i]);
                }
            }
        }
    }
}
