using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseToExportFieldsInManifest", typeof(Strings), nameof(Strings.UseToExportFieldsInManifestDescription))]
    internal class UseToExportFieldsInManifest : ScriptRule
    {
        private static readonly string[] s_exportFields = new[]
        {
            "FunctionsToExport",
            "CmdletsToExport",
            "AliasesToExport",
        };

        private const int MaxLineLength = 80;
        private const int TabWidth = 4;
        private const int ContinuationIndentWidth = TabWidth * 2;

        internal UseToExportFieldsInManifest(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (scriptPath is null || !AstExtensions.IsModuleManifest(scriptPath))
            {
                yield break;
            }

            HashtableAst? hashtable = ast
                .FindAll(static node => node is HashtableAst, searchNestedScriptBlocks: false)
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

            IReadOnlyList<string>? moduleFunctions = null;
            string? rootModulePath = ResolveRootModulePath(hashtable, scriptPath);

            if (rootModulePath is not null)
            {
                moduleFunctions = GetExportedFunctionNames(rootModulePath);
            }

            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is not StringConstantExpressionAst keyAst)
                {
                    continue;
                }

                string? fieldName = null;
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

                IReadOnlyList<string>? exportNames = string.Equals(fieldName, "FunctionsToExport", StringComparison.OrdinalIgnoreCase)
                    ? moduleFunctions
                    : null;

                foreach (ScriptDiagnostic diagnostic in CheckExportFieldValue(kvp.Item2, fieldName, exportNames))
                {
                    yield return diagnostic;
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> CheckExportFieldValue(StatementAst valueStatement, string fieldName, IReadOnlyList<string>? exportNames)
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
                yield return CreateDiagnosticWithCorrection(fieldName, varExpr, exportNames);
                yield break;
            }

            if (valueAst is StringConstantExpressionAst stringConst && ContainsWildcard(stringConst.Value))
            {
                yield return CreateDiagnosticWithCorrection(fieldName, stringConst, exportNames);
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

        private ScriptDiagnostic CreateDiagnosticWithCorrection(string fieldName, Ast extent, IReadOnlyList<string>? exportNames)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Strings.UseToExportFieldsInManifestError, fieldName);

            if (exportNames is null || exportNames.Count == 0)
            {
                return CreateDiagnostic(message, extent);
            }

            string correctionText = FormatExportArray(exportNames);
            string extentText = extent.Extent.Text;
            if (extentText.Length >= 2
                && extentText[0] == '\''
                && extentText[extentText.Length - 1] == '\'')
            {
                extentText = extentText.Substring(1, extentText.Length - 2);
            }
            string description = string.Format(CultureInfo.CurrentCulture, "Replace '{0}' with {1}", extentText, correctionText);

            var correction = new Correction(extent.Extent, correctionText, description);

            return CreateDiagnostic(message, extent.Extent, new[] { correction });
        }

        private static string FormatExportArray(IReadOnlyList<string> names)
        {
            if (names.Count == 0)
            {
                return "@()";
            }

            var sb = new StringBuilder();
            sb.Append("@(");
            int currentLineLength = 2;

            for (int i = 0; i < names.Count; i++)
            {
                string element = "'" + names[i] + "'";
                int elementWithSeparator = element.Length + 2; // always count ", "

                if (i > 0 && currentLineLength + elementWithSeparator >= MaxLineLength)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append("\t\t");
                    currentLineLength = ContinuationIndentWidth;
                }

                sb.Append(element);
                if (i < names.Count - 1)
                {
                    sb.Append(", ");
                }
                currentLineLength += elementWithSeparator;
            }

            sb.Append(')');
            return sb.ToString();
        }

        private static string? ResolveRootModulePath(HashtableAst hashtable, string manifestPath)
        {
            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is not StringConstantExpressionAst keyAst)
                {
                    continue;
                }

                if (!string.Equals(keyAst.Value, "RootModule", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(keyAst.Value, "ModuleToProcess", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (kvp.Item2 is PipelineAst rhsPipeline)
                {
                    ExpressionAst? expr = rhsPipeline.GetPureExpression();
                    if (expr is StringConstantExpressionAst strValue && !string.IsNullOrWhiteSpace(strValue.Value))
                    {
                        string? manifestDir = Path.GetDirectoryName(manifestPath);
                        if (manifestDir is not null)
                        {
                            string fullPath = Path.Combine(manifestDir, strValue.Value);
                            if (File.Exists(fullPath))
                            {
                                return fullPath;
                            }
                        }
                    }
                }

                break;
            }

            return null;
        }

        private static IReadOnlyList<string>? GetExportedFunctionNames(string modulePath)
        {
            try
            {
                string content = File.ReadAllText(modulePath);
                Ast moduleAst = Parser.ParseInput(content, out _, out _);

                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                // Collect function definitions
                foreach (Ast node in moduleAst.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: false))
                {
                    var funcDef = (FunctionDefinitionAst)node;
                    if (!string.IsNullOrWhiteSpace(funcDef.Name))
                    {
                        names.Add(funcDef.Name);
                    }
                }

                return names.Count > 0 ? names.ToList() : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool ContainsWildcard(string? value)
        {
            return value is not null && value.IndexOfAny(new[] { '*', '?' }) >= 0;
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
