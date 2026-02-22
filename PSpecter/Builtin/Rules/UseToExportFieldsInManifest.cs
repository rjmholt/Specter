#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseToExportFieldsInManifest", typeof(Strings), nameof(Strings.UseToExportFieldsInManifestDescription))]
    public class UseToExportFieldsInManifest : ScriptRule
    {
        private static readonly string[] s_exportFields = new[]
        {
            "FunctionsToExport",
            "CmdletsToExport",
            "AliasesToExport",
        };

        public UseToExportFieldsInManifest(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (fileName is null || !AstExtensions.IsModuleManifest(fileName))
            {
                yield break;
            }

            HashtableAst hashtable = ast
                .FindAll(node => node is HashtableAst, searchNestedScriptBlocks: false)
                .OfType<HashtableAst>()
                .FirstOrDefault();

            if (hashtable is null)
            {
                yield break;
            }

            if (!HasModuleVersion(hashtable))
            {
                yield break;
            }

            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is not StringConstantExpressionAst keyAst)
                {
                    continue;
                }

                string fieldName = null;
                foreach (string exportField in s_exportFields)
                {
                    if (string.Equals(keyAst.Value, exportField, StringComparison.OrdinalIgnoreCase))
                    {
                        fieldName = exportField;
                        break;
                    }
                }

                if (fieldName is null)
                {
                    continue;
                }

                foreach (ScriptDiagnostic diagnostic in CheckExportFieldValue(kvp.Item2, fieldName))
                {
                    yield return diagnostic;
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> CheckExportFieldValue(StatementAst valueStatement, string fieldName)
        {
            if (valueStatement is null)
            {
                yield break;
            }

            Ast valueAst = valueStatement is PipelineAst pipeline
                ? pipeline.GetPureExpression()
                : valueStatement;

            if (valueAst is null)
            {
                yield break;
            }

            if (valueAst is VariableExpressionAst varExpr
                && varExpr.VariablePath.UserPath.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName),
                    varExpr);
                yield break;
            }

            if (valueAst is StringConstantExpressionAst stringConst && ContainsWildcard(stringConst.Value))
            {
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName),
                    stringConst);
                yield break;
            }

            if (valueAst is ArrayLiteralAst arrayLiteral)
            {
                foreach (ExpressionAst element in arrayLiteral.Elements)
                {
                    if (element is StringConstantExpressionAst elemStr && ContainsWildcard(elemStr.Value))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName),
                            elemStr);
                    }
                }
                yield break;
            }

            if (valueAst is ArrayExpressionAst arrayExpr)
            {
                foreach (StatementAst stmt in arrayExpr.SubExpression.Statements)
                {
                    if (stmt is not PipelineAst elemPipeline)
                    {
                        continue;
                    }

                    ExpressionAst pureExpr = elemPipeline.GetPureExpression();
                    if (pureExpr is null)
                    {
                        continue;
                    }

                    if (pureExpr is StringConstantExpressionAst strElem && ContainsWildcard(strElem.Value))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName),
                            strElem);
                    }
                    else if (pureExpr is ArrayLiteralAst innerArray)
                    {
                        foreach (ExpressionAst innerElem in innerArray.Elements)
                        {
                            if (innerElem is StringConstantExpressionAst innerStr && ContainsWildcard(innerStr.Value))
                            {
                                yield return CreateDiagnostic(
                                    string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName),
                                    innerStr);
                            }
                        }
                    }
                }
            }
        }

        private static bool ContainsWildcard(string value)
        {
            return value.IndexOfAny(new[] { '*', '?' }) >= 0;
        }

        private static bool HasModuleVersion(HashtableAst hashtable)
        {
            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyAst
                    && string.Equals(keyAst.Value, "ModuleVersion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
