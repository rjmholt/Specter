using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseIdenticalMandatoryParametersForDSC", typeof(Strings), nameof(Strings.UseIdenticalMandatoryParametersDSCDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Error)]
    internal class UseIdenticalMandatoryParametersDsc : ScriptRule
    {
        internal UseIdenticalMandatoryParametersDsc(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                yield break;
            }

            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);
            if (dscFuncs.Count == 0)
            {
                yield break;
            }

            string? mofPath = GetMofFilePath(scriptPath!);
            if (mofPath is null)
            {
                yield break;
            }

            IDictionary<string, string> keyRequiredProps = MofParser.GetKeyAndRequiredProperties(mofPath);
            if (keyRequiredProps.Count == 0)
            {
                yield break;
            }

            foreach (FunctionDefinitionAst func in dscFuncs)
            {
                var mandatoryParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ParameterAst param in GetMandatoryParameters(func))
                {
                    mandatoryParams.Add(param.Name.VariablePath.UserPath);
                }

                foreach (string key in keyRequiredProps.Keys)
                {
                    if (!mandatoryParams.Contains(key))
                    {
                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                Strings.UseIdenticalMandatoryParametersDSCError,
                                keyRequiredProps[key],
                                key,
                                func.Name),
                            GetFunctionNameExtent(func));
                    }
                }
            }
        }

        private static string? GetMofFilePath(string filePath)
        {
            string? directoryName = Path.GetDirectoryName(filePath);
            if (directoryName is null)
            {
                return null;
            }

            string mofPath = Path.Combine(
                directoryName,
                Path.GetFileNameWithoutExtension(filePath)) + ".schema.mof";

            return File.Exists(mofPath) ? mofPath : null;
        }

        private static IEnumerable<ParameterAst> GetMandatoryParameters(FunctionDefinitionAst func)
        {
            IReadOnlyList<ParameterAst>? parameters = null;

            if (func.Body?.ParamBlock?.Parameters is not null)
            {
                parameters = func.Body.ParamBlock.Parameters;
            }
            else if (func.Parameters is not null)
            {
                parameters = func.Parameters;
            }

            if (parameters is null)
            {
                yield break;
            }

            foreach (ParameterAst param in parameters)
            {
                if (IsParameterMandatory(param))
                {
                    yield return param;
                }
            }
        }

        private static bool IsParameterMandatory(ParameterAst param)
        {
            foreach (AttributeBaseAst attr in param.Attributes)
            {
                if (attr is not AttributeAst attrAst)
                {
                    continue;
                }

                if (!IsParameterAttribute(attrAst))
                {
                    continue;
                }

                if (attrAst.NamedArguments is null)
                {
                    continue;
                }

                foreach (NamedAttributeArgumentAst namedArg in attrAst.NamedArguments)
                {
                    if (namedArg.ArgumentName.Equals("Mandatory", StringComparison.OrdinalIgnoreCase)
                        && IsTrueExpression(namedArg))
                    {
                        return true;
                    }
                }

                if (attrAst.NamedArguments.Count == 0 && attrAst.PositionalArguments.Count == 0)
                {
                    continue;
                }
            }

            return false;
        }

        private static bool IsParameterAttribute(AttributeAst attr)
        {
            string name = attr.TypeName.Name;
            return name.Equals("parameter", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("Parameter", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrueExpression(NamedAttributeArgumentAst namedArg)
        {
            if (namedArg.ExpressionOmitted)
            {
                return true;
            }

            if (namedArg.Argument is VariableExpressionAst varExpr)
            {
                return varExpr.VariablePath.UserPath.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (namedArg.Argument is ConstantExpressionAst constExpr && constExpr.Value is bool boolVal)
            {
                return boolVal;
            }

            return false;
        }

        private static IScriptExtent GetFunctionNameExtent(FunctionDefinitionAst func)
        {
            string funcText = func.Extent.Text;
            int nameIndex = funcText.IndexOf(func.Name, StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0)
            {
                return func.Extent;
            }

            string? fullScript = func.Extent.StartScriptPosition.GetFullScript();
            if (fullScript is null)
            {
                return func.Extent;
            }

            int nameStart = func.Extent.StartOffset + nameIndex;
            int nameEnd = nameStart + func.Name.Length;

            return ScriptExtent.FromOffsets(fullScript, func.Extent.File, nameStart, nameEnd);
        }
    }
}
