using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("DscExamplesPresent", typeof(Strings), nameof(Strings.DscExamplesPresentDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Information)]
    internal class DscExamplesPresent : ScriptRule
    {
        internal DscExamplesPresent(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                yield break;
            }

            foreach (ScriptDiagnostic diag in AnalyzeResourceModule(ast, scriptPath!))
            {
                yield return diag;
            }

            foreach (ScriptDiagnostic diag in AnalyzeDscClasses(ast, scriptPath!))
            {
                yield return diag;
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeResourceModule(Ast ast, string scriptPath)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);
            if (dscFuncs.Count == 0)
            {
                yield break;
            }

            string resourceName = Path.GetFileNameWithoutExtension(scriptPath);
            string examplesPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scriptPath)!, "..", "..", "Examples"));

            if (!HasMatchingFiles(examplesPath, resourceName))
            {
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.DscExamplesPresentNoExamplesError, resourceName),
                    ast.Extent);
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeDscClasses(Ast ast, string scriptPath)
        {
            IReadOnlyList<TypeDefinitionAst> dscClasses = DscResourceHelper.GetDscClasses(ast);

            foreach (TypeDefinitionAst dscClass in dscClasses)
            {
                string resourceName = dscClass.Name;
                string examplesPath = Path.Combine(Path.GetDirectoryName(scriptPath)!, "Examples");

                if (!HasMatchingFiles(examplesPath, resourceName))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.DscExamplesPresentNoExamplesError, resourceName),
                        dscClass.Extent);
                }
            }
        }

        private static bool HasMatchingFiles(string directoryPath, string resourceName)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                var dir = new DirectoryInfo(directoryPath);
                return dir.GetFiles($"*{resourceName}*").Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
