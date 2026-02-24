using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidFilterKeyword", typeof(Strings), nameof(Strings.AvoidFilterKeywordDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidFilterKeyword : ScriptRule
    {
        internal AvoidFilterKeyword(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is FunctionDefinitionAst func && func.IsFilter, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)found;

                yield return CreateDiagnostic(
                    string.Format(Strings.AvoidFilterKeywordError, funcAst.Name),
                    funcAst.Extent);
            }
        }
    }
}
