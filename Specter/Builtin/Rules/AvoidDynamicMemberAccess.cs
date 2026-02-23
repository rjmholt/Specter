using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidDynamicMemberAccess", typeof(Strings), nameof(Strings.AvoidDynamicMemberAccessDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidDynamicMemberAccess : ScriptRule
    {
        internal AvoidDynamicMemberAccess(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is MemberExpressionAst, searchNestedScriptBlocks: true))
            {
                var memberAst = (MemberExpressionAst)found;

                if (memberAst.Member is ConstantExpressionAst)
                {
                    continue;
                }

                string accessKind = memberAst is InvokeMemberExpressionAst ? "method" : "property";

                yield return CreateDiagnostic(
                    string.Format(Strings.AvoidDynamicMemberAccessError, accessKind),
                    memberAst.Extent);
            }
        }
    }
}
