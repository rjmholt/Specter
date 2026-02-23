using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("PossibleIncorrectUsageOfRedirectionOperator", typeof(Strings), nameof(Strings.PossibleIncorrectUsageOfRedirectionOperatorDescription))]
    internal class PossibleIncorrectUsageOfRedirectionOperator : ScriptRule
    {
        internal PossibleIncorrectUsageOfRedirectionOperator(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is IfStatementAst, searchNestedScriptBlocks: true))
            {
                var ifStatement = (IfStatementAst)node;

                foreach (var clause in ifStatement.Clauses)
                {
                    var fileRedirection = clause.Item1.Find(
                        testAst => testAst is FileRedirectionAst,
                        searchNestedScriptBlocks: false) as FileRedirectionAst;

                    if (fileRedirection != null)
                    {
                        yield return CreateDiagnostic(
                            Strings.PossibleIncorrectUsageOfRedirectionOperatorError,
                            fileRedirection.Extent);
                    }
                }
            }
        }
    }
}
