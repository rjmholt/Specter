using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using PSpecter.Configuration.Psd;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer
{
    /// <summary>
    /// Compatibility shim providing static helper methods that the original
    /// PSScriptAnalyzer exposed on its Helper class.
    /// Delegates to the engine's <see cref="PsdDataParser"/> for AST evaluation.
    /// </summary>
    public static class Helper
    {
        private static readonly PsdDataParser s_parser = new PsdDataParser();

        private static readonly HashSet<string> s_manifestKeysV3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RootModule", "ModuleToProcess", "ModuleVersion", "GUID", "Author", "CompanyName",
            "Copyright", "Description", "PowerShellVersion", "PowerShellHostName",
            "PowerShellHostVersion", "DotNetFrameworkVersion", "CLRVersion",
            "ProcessorArchitecture", "RequiredModules", "RequiredAssemblies",
            "ScriptsToProcess", "TypesToProcess", "FormatsToProcess", "NestedModules",
            "FunctionsToExport", "CmdletsToExport", "VariablesToExport", "AliasesToExport",
            "ModuleList", "FileList", "PrivateData", "HelpInfoURI", "DefaultCommandPrefix",
        };

        private static readonly HashSet<string> s_manifestKeysV4Plus = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DscResourcesToExport",
        };

        /// <summary>
        /// Safely evaluates a PowerShell expression AST to a .NET value,
        /// provided the expression is statically evaluable (constants, arrays, hashtables, $true/$false/$null).
        /// </summary>
        public static object? GetSafeValueFromExpressionAst(ExpressionAst exprAst)
        {
            if (exprAst == null)
            {
                throw new ArgumentNullException(nameof(exprAst));
            }

            return s_parser.ConvertAstValue(exprAst);
        }

        internal static Hashtable? GetSafeValueFromHashtableAst(HashtableAst hashtableAst)
        {
            return s_parser.ConvertAstValue(hashtableAst);
        }

        /// <summary>
        /// Determines whether the given PSD1 file is a valid module manifest
        /// for the specified PowerShell version.
        /// </summary>
        public static bool IsModuleManifest(string filePath, Version psVersion)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            string content;
            try
            {
                content = File.ReadAllText(filePath);
            }
            catch
            {
                return false;
            }

            Ast ast = Parser.ParseInput(content, out _, out ParseError[] errors);
            if (errors != null && errors.Length > 0)
            {
                return false;
            }

            HashtableAst? hashtableAst = FindTopLevelHashtable(ast);
            if (hashtableAst == null)
            {
                return false;
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in hashtableAst.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyAst)
                {
                    keys.Add(keyAst.Value);
                }
            }

            if (!keys.Contains("ModuleVersion"))
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (s_manifestKeysV3.Contains(key))
                {
                    continue;
                }

                if (s_manifestKeysV4Plus.Contains(key))
                {
                    if (psVersion.Major < 4)
                    {
                        return false;
                    }
                    continue;
                }
            }

            return true;
        }

        private static HashtableAst? FindTopLevelHashtable(Ast ast)
        {
            if (ast is ScriptBlockAst scriptBlock
                && scriptBlock.EndBlock?.Statements.Count == 1
                && scriptBlock.EndBlock.Statements[0] is PipelineAst pipeline
                && pipeline.PipelineElements.Count == 1
                && pipeline.PipelineElements[0] is CommandExpressionAst cmdExpr
                && cmdExpr.Expression is HashtableAst ht)
            {
                return ht;
            }

            return null;
        }
    }
}
