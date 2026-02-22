#nullable disable

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
    [Rule("DscTestsPresent", typeof(Strings), nameof(Strings.DscTestsPresentDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Information)]
    public class DscTestsPresent : ScriptRule
    {
        public DscTestsPresent(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                yield break;
            }

            foreach (ScriptDiagnostic diag in AnalyzeResourceModule(ast, fileName))
            {
                yield return diag;
            }

            foreach (ScriptDiagnostic diag in AnalyzeDscClasses(ast, fileName))
            {
                yield return diag;
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeResourceModule(Ast ast, string fileName)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);
            if (dscFuncs.Count == 0)
            {
                yield break;
            }

            string resourceName = Path.GetFileNameWithoutExtension(fileName);
            string testsPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName), "..", "..", "Tests"));

            if (!HasMatchingFiles(testsPath, resourceName, searchSubdirectories: true))
            {
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.DscTestsPresentNoTestsError, resourceName),
                    ast.Extent);
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeDscClasses(Ast ast, string fileName)
        {
            IReadOnlyList<TypeDefinitionAst> dscClasses = DscResourceHelper.GetDscClasses(ast);

            foreach (TypeDefinitionAst dscClass in dscClasses)
            {
                string resourceName = dscClass.Name;
                string testsPath = Path.Combine(Path.GetDirectoryName(fileName), "Tests");

                if (!HasMatchingFiles(testsPath, resourceName, searchSubdirectories: false))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.DscTestsPresentNoTestsError, resourceName),
                        dscClass.Extent);
                }
            }
        }

        private static bool HasMatchingFiles(string directoryPath, string resourceName, bool searchSubdirectories)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                var dir = new DirectoryInfo(directoryPath);
                string pattern = $"*{resourceName}*";
                SearchOption option = searchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                return dir.GetFiles(pattern, option).Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
