using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("StandardDSCFunctionsInResource", typeof(Strings), nameof(Strings.UseStandardDSCFunctionsInResourceDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Error)]
    internal class UseStandardDscFunctionsInResource : ScriptRule
    {
        internal UseStandardDscFunctionsInResource(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            foreach (ScriptDiagnostic diag in AnalyzeResourceFunctions(ast))
            {
                yield return diag;
            }

            foreach (ScriptDiagnostic diag in AnalyzeDscClasses(ast))
            {
                yield return diag;
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeResourceFunctions(Ast ast)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);
            if (dscFuncs.Count == 0)
            {
                yield break;
            }

            var foundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FunctionDefinitionAst func in dscFuncs)
            {
                foundNames.Add(func.Name);
            }

            foreach (string expected in DscResourceHelper.TargetResourceFunctionNames)
            {
                if (!foundNames.Contains(expected))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.UseStandardDSCFunctionsInResourceError, expected),
                        ast.Extent);
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeDscClasses(Ast ast)
        {
            IReadOnlyList<TypeDefinitionAst> dscClasses = DscResourceHelper.GetDscClasses(ast);
            foreach (TypeDefinitionAst dscClass in dscClasses)
            {
                foreach (string methodName in DscResourceHelper.ClassResourceMethodNames)
                {
                    if (DscResourceHelper.FindClassMethod(dscClass, methodName) is null)
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.UseStandardDSCFunctionsInClassError, methodName),
                            dscClass.Extent);
                    }
                }
            }
        }
    }
}
