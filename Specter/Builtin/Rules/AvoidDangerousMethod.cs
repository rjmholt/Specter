using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidDangerousMethod", typeof(Strings), nameof(Strings.AvoidDangerousMethodDescription))]
    internal class AvoidDangerousMethod : ScriptRule
    {
        private static readonly HashSet<string> s_dangerousMethodNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "InvokeScript",
            "CreateNestedPipeline",
            "AddScript",
            "NewScriptBlock",
            "ExpandString",
        };

        internal AvoidDangerousMethod(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is InvokeMemberExpressionAst, searchNestedScriptBlocks: true))
            {
                var invokeAst = (InvokeMemberExpressionAst)found;

                if (invokeAst.Member is not StringConstantExpressionAst memberName)
                {
                    continue;
                }

                string name = memberName.Value;

                if (s_dangerousMethodNames.Contains(name))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidDangerousMethodError, name),
                        invokeAst.Extent);
                    continue;
                }

                if (name.Equals("Create", StringComparison.OrdinalIgnoreCase)
                    && IsScriptBlockType(invokeAst.Expression))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidDangerousMethodError, "ScriptBlock.Create"),
                        invokeAst.Extent);
                }
            }
        }

        private static bool IsScriptBlockType(ExpressionAst expression)
        {
            if (expression is TypeExpressionAst typeExpr)
            {
                string typeName = typeExpr.TypeName.FullName;
                return typeName.Equals("scriptblock", StringComparison.OrdinalIgnoreCase)
                    || typeName.Equals("System.Management.Automation.ScriptBlock", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
