using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidEmptyCatchBlock: Check if any empty catch block is used.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingEmptyCatchBlock", typeof(Strings), nameof(Strings.AvoidUsingEmptyCatchBlockDescription))]
    internal class AvoidEmptyCatchBlock : ScriptRule
    {
        internal AvoidEmptyCatchBlock(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check if any empty catch block is used.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            // Finds all CommandAsts.
            IEnumerable<Ast> foundAsts = ast.FindAll(static testAst => testAst is CatchClauseAst, true);

            // Iterates all CatchClauseAst and check the statements count.
            foreach (Ast foundAst in foundAsts)
            {
                CatchClauseAst catchAst = (CatchClauseAst)foundAst;

                if (catchAst.Body.Statements.Count == 0)
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidEmptyCatchBlockError),
                        catchAst);
                }
            }
        }
    }
}




