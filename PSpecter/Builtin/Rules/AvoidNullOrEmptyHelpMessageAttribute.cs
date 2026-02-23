using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidNullOrEmptyHelpMessageAttribute", typeof(Strings), nameof(Strings.AvoidNullOrEmptyHelpMessageAttributeDescription))]
    internal class AvoidNullOrEmptyHelpMessageAttribute : ScriptRule
    {
        public AvoidNullOrEmptyHelpMessageAttribute(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast foundAst in ast.FindAll(static testAst => testAst is ParameterAst, searchNestedScriptBlocks: true))
            {
                var paramAst = (ParameterAst)foundAst;

                foreach (AttributeBaseAst attrBase in paramAst.Attributes)
                {
                    if (attrBase is not AttributeAst attr)
                    {
                        continue;
                    }

                    foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
                    {
                        if (!string.Equals(namedArg.ArgumentName, "HelpMessage", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (namedArg.ExpressionOmitted || IsEmptyString(namedArg.Argument) || IsNull(namedArg.Argument))
                        {
                            yield return CreateDiagnostic(Strings.AvoidNullOrEmptyHelpMessageAttributeError, namedArg);
                        }
                    }
                }
            }
        }

        private static bool IsEmptyString(ExpressionAst expr)
        {
            return expr is StringConstantExpressionAst strConst && strConst.Value.Length == 0;
        }

        private static bool IsNull(ExpressionAst expr)
        {
            return expr is VariableExpressionAst varExpr
                && string.Equals(varExpr.VariablePath.UserPath, "null", StringComparison.OrdinalIgnoreCase);
        }
    }
}
