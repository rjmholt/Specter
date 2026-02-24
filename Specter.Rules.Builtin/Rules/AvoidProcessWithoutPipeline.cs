using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidProcessWithoutPipeline", typeof(Strings), nameof(Strings.AvoidProcessWithoutPipelineDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidProcessWithoutPipeline : ScriptRule
    {
        internal AvoidProcessWithoutPipeline(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)found;
                NamedBlockAst? processBlock = funcAst.Body.ProcessBlock;

                if (processBlock is null || processBlock.Unnamed)
                {
                    continue;
                }

                if (HasPipelineParameter(funcAst))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(Strings.AvoidProcessWithoutPipelineError, funcAst.Name),
                    processBlock.Extent);
            }
        }

        private static bool HasPipelineParameter(FunctionDefinitionAst funcAst)
        {
            ParamBlockAst? paramBlock = funcAst.Body.ParamBlock;
            if (paramBlock?.Parameters is null)
            {
                return false;
            }

            foreach (ParameterAst param in paramBlock.Parameters)
            {
                foreach (AttributeBaseAst attr in param.Attributes)
                {
                    if (attr is not AttributeAst attrAst)
                    {
                        continue;
                    }

                    if (!attrAst.TypeName.FullName.Equals("Parameter", StringComparison.OrdinalIgnoreCase)
                        && !attrAst.TypeName.FullName.Equals("System.Management.Automation.ParameterAttribute", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (NamedAttributeArgumentAst namedArg in attrAst.NamedArguments)
                    {
                        if (namedArg.ArgumentName.Equals("ValueFromPipeline", StringComparison.OrdinalIgnoreCase)
                            || namedArg.ArgumentName.Equals("ValueFromPipelineByPropertyName", StringComparison.OrdinalIgnoreCase))
                        {
                            if (namedArg.ExpressionOmitted || IsTrue(namedArg.Argument))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsTrue(ExpressionAst expression)
        {
            if (expression is VariableExpressionAst varExpr)
            {
                return varExpr.VariablePath.UserPath.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (expression is ConstantExpressionAst constant && constant.Value is bool boolVal)
            {
                return boolVal;
            }

            return false;
        }
    }
}
