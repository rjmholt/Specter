using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidInvokingEmptyMembers", typeof(Strings), nameof(Strings.AvoidInvokingEmptyMembersDescription))]
    internal class AvoidInvokingEmptyMembers : ScriptRule
    {
        internal AvoidInvokingEmptyMembers(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is MemberExpressionAst, searchNestedScriptBlocks: true))
            {
                var member = (MemberExpressionAst)node;
                string context = member.Member?.Extent.ToString() ?? string.Empty;

                if (!context.Contains("("))
                {
                    continue;
                }

                IEnumerable<Ast> binaryExpressions = member.FindAll(
                static binaryAst => binaryAst is BinaryExpressionAst, searchNestedScriptBlocks: true);

                if (binaryExpressions.Any())
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidInvokingEmptyMembersError, context),
                        member.Extent);
                }
            }
        }
    }
}
