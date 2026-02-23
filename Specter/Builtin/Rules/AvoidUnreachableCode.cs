using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUnreachableCode", typeof(Strings), nameof(Strings.AvoidUnreachableCodeDescription))]
    internal class AvoidUnreachableCode : ScriptRule
    {
        internal AvoidUnreachableCode(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is NamedBlockAst, searchNestedScriptBlocks: true))
            {
                foreach (ScriptDiagnostic diag in CheckStatements(((NamedBlockAst)found).Statements))
                {
                    yield return diag;
                }
            }

            foreach (Ast found in ast.FindAll(static a => a is StatementBlockAst, searchNestedScriptBlocks: true))
            {
                foreach (ScriptDiagnostic diag in CheckStatements(((StatementBlockAst)found).Statements))
                {
                    yield return diag;
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> CheckStatements(IReadOnlyList<StatementAst> statements)
        {
            bool afterTerminator = false;
            for (int i = 0; i < statements.Count; i++)
            {
                if (afterTerminator)
                {
                    yield return CreateDiagnostic(
                        Strings.AvoidUnreachableCodeError,
                        statements[i].Extent);
                    yield break;
                }

                if (IsUnconditionalTerminator(statements[i]))
                {
                    afterTerminator = true;
                }
            }
        }

        private static bool IsUnconditionalTerminator(StatementAst statement)
        {
            return statement is ReturnStatementAst
                || statement is ThrowStatementAst
                || statement is ExitStatementAst
                || statement is BreakStatementAst
                || statement is ContinueStatementAst;
        }
    }
}
