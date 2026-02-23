using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using Specter.Logging;

namespace Specter.Security
{
    internal readonly struct ManifestAuditResult
    {
        internal ManifestAuditResult(bool isValid, string? rejectionReason = null)
        {
            IsValid = isValid;
            RejectionReason = rejectionReason;
        }

        internal bool IsValid { get; }
        internal string? RejectionReason { get; }
    }

    internal static class ModuleManifestAuditor
    {
        private static readonly string[] s_dangerousFields = new[]
        {
            "ScriptsToProcess",
            "TypesToProcess",
            "FormatsToProcess",
            "RequiredAssemblies",
        };

        internal static ManifestAuditResult Audit(string manifestPath, IAnalysisLogger? logger)
        {
            if (!File.Exists(manifestPath))
            {
                return new ManifestAuditResult(false, $"Manifest file not found: '{manifestPath}'.");
            }

            Hashtable? manifestData;
            try
            {
                manifestData = ParseManifest(manifestPath);
            }
            catch (Exception ex)
            {
                string msg = $"Failed to parse manifest '{manifestPath}': {ex.Message}";
                logger?.Warning(msg);
                return new ManifestAuditResult(false, msg);
            }

            if (manifestData is null)
            {
                return new ManifestAuditResult(false, $"Manifest '{manifestPath}' did not parse as a hashtable.");
            }

            foreach (string field in s_dangerousFields)
            {
                if (!manifestData.ContainsKey(field))
                {
                    continue;
                }

                object? value = manifestData[field];
                if (value is null)
                {
                    continue;
                }

                if (IsEmptyOrWhitespace(value))
                {
                    continue;
                }

                string msg = $"Module manifest '{manifestPath}' contains a non-empty '{field}' field. " +
                             $"This field can execute arbitrary code during import and is rejected by Specter's security policy.";
                logger?.Warning(msg);
                return new ManifestAuditResult(false, msg);
            }

            ManifestAuditResult nestedResult = AuditNestedModules(manifestData, manifestPath, logger);
            if (!nestedResult.IsValid)
            {
                return nestedResult;
            }

            return new ManifestAuditResult(true);
        }

        private static bool IsEmptyOrWhitespace(object? value)
        {
            if (value is null)
            {
                return true;
            }

            if (value is object[] array && array.Length == 0)
            {
                return true;
            }

            if (value is string str)
            {
                string trimmed = str.Trim();
                return string.IsNullOrWhiteSpace(trimmed) || trimmed == "@()";
            }

            return false;
        }

        private static ManifestAuditResult AuditNestedModules(Hashtable manifest, string manifestPath, IAnalysisLogger? logger)
        {
            if (!manifest.ContainsKey("NestedModules"))
            {
                return new ManifestAuditResult(true);
            }

            object? nestedValue = manifest["NestedModules"];
            if (nestedValue is null)
            {
                return new ManifestAuditResult(true);
            }

            string moduleDir = Path.GetDirectoryName(manifestPath) ?? ".";
            string canonicalModuleDir = Path.GetFullPath(moduleDir);

            string[]? paths = null;
            if (nestedValue is string singlePath)
            {
                paths = new[] { singlePath };
            }
            else if (nestedValue is object[] array)
            {
                paths = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    paths[i] = array[i]?.ToString() ?? string.Empty;
                }
            }

            if (paths is null)
            {
                return new ManifestAuditResult(true);
            }

            foreach (string nestedPath in paths)
            {
                if (string.IsNullOrWhiteSpace(nestedPath))
                {
                    continue;
                }

                string fullNestedPath = Path.IsPathRooted(nestedPath)
                    ? Path.GetFullPath(nestedPath)
                    : Path.GetFullPath(Path.Combine(canonicalModuleDir, nestedPath));

                string normalizedNested = NormalizeDirPath(fullNestedPath);
                string normalizedModule = NormalizeDirPath(canonicalModuleDir);

                if (!normalizedNested.StartsWith(normalizedModule, PathComparison))
                {
                    string msg = $"Module manifest '{manifestPath}' contains NestedModules entry '{nestedPath}' " +
                                 $"that resolves outside the module directory.";
                    logger?.Warning(msg);
                    return new ManifestAuditResult(false, msg);
                }
            }

            return new ManifestAuditResult(true);
        }

        private static Hashtable? ParseManifest(string path)
        {
            string content = File.ReadAllText(path);
            Token[] tokens;
            ParseError[] errors;
            var ast = Parser.ParseInput(content, out tokens, out errors);

            if (errors.Length > 0)
            {
                return null;
            }

            var pipeline = ast.EndBlock?.Statements;
            if (pipeline is null || pipeline.Count == 0)
            {
                return null;
            }

            if (pipeline[0] is PipelineAst pipelineAst
                && pipelineAst.PipelineElements.Count == 1
                && pipelineAst.PipelineElements[0] is CommandExpressionAst cmdExpr
                && cmdExpr.Expression is HashtableAst hashtableAst)
            {
                return ConvertHashtableAst(hashtableAst);
            }

            return null;
        }

        private static Hashtable ConvertHashtableAst(HashtableAst hashtableAst)
        {
            var result = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in hashtableAst.KeyValuePairs)
            {
                string? key = GetConstantValue(kvp.Item1);
                if (key is null)
                {
                    continue;
                }

                object? value = GetExpressionValue(kvp.Item2);
                result[key] = value;
            }

            return result;
        }

        private static string? GetConstantValue(Ast ast)
        {
            if (ast is StringConstantExpressionAst str)
            {
                return str.Value;
            }

            return ast.Extent.Text;
        }

        private static object? GetExpressionValue(Ast ast)
        {
            if (ast is PipelineAst pipelineAst
                && pipelineAst.PipelineElements.Count == 1
                && pipelineAst.PipelineElements[0] is CommandExpressionAst cmdExprAst)
            {
                return GetExpressionValue(cmdExprAst.Expression);
            }

            if (ast is StringConstantExpressionAst str)
            {
                return str.Value;
            }

            if (ast is ArrayLiteralAst arrayLiteral)
            {
                var items = new object[arrayLiteral.Elements.Count];
                for (int i = 0; i < arrayLiteral.Elements.Count; i++)
                {
                    items[i] = GetExpressionValue(arrayLiteral.Elements[i])!;
                }
                return items;
            }

            if (ast is ArrayExpressionAst arrayExpr)
            {
                if (arrayExpr.SubExpression.Statements.Count == 0)
                {
                    return Array.Empty<object>();
                }

                var items = new List<object>();
                foreach (StatementAst statement in arrayExpr.SubExpression.Statements)
                {
                    if (statement is PipelineAst pipeline
                        && pipeline.PipelineElements.Count == 1
                        && pipeline.PipelineElements[0] is CommandExpressionAst cmdExprInner)
                    {
                        object? inner = GetExpressionValue(cmdExprInner.Expression);
                        if (inner is object[] innerArr)
                        {
                            for (int j = 0; j < innerArr.Length; j++)
                            {
                                items.Add(innerArr[j]);
                            }
                        }
                        else if (inner is not null)
                        {
                            items.Add(inner);
                        }
                    }
                }

                return items.Count > 0 ? items.ToArray() : Array.Empty<object>();
            }

            if (ast is HashtableAst nestedHash)
            {
                return ConvertHashtableAst(nestedHash);
            }

            return ast.Extent.Text;
        }

        private static string NormalizeDirPath(string path)
        {
            if (path.Length > 0
                && path[path.Length - 1] != Path.DirectorySeparatorChar
                && path[path.Length - 1] != Path.AltDirectorySeparatorChar)
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }

        private static StringComparison PathComparison =>
#if CORECLR
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
#else
            StringComparison.OrdinalIgnoreCase;
#endif
    }
}
