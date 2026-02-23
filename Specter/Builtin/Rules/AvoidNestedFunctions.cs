using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidNestedFunctions", typeof(Strings), nameof(Strings.AvoidNestedFunctionsDescription))]
    internal class AvoidNestedFunctions : ScriptRule
    {
        internal AvoidNestedFunctions(RuleInfo ruleInfo)
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

                string? outerName = FindOuterFunctionName(funcAst);
                if (outerName is null)
                {
                    continue;
                }

                if (IsInsideDscConfiguration(funcAst))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(Strings.AvoidNestedFunctionsError, funcAst.Name, outerName),
                    funcAst.Extent);
            }
        }

        private static string? FindOuterFunctionName(FunctionDefinitionAst funcAst)
        {
            Ast? current = funcAst.Parent;
            while (current is not null)
            {
                if (current is FunctionDefinitionAst outerFunc)
                {
                    return outerFunc.Name;
                }

                current = current.Parent;
            }

            return null;
        }

        private static bool IsInsideDscConfiguration(FunctionDefinitionAst funcAst)
        {
            Ast? current = funcAst.Parent;
            while (current is not null)
            {
                if (current is ConfigurationDefinitionAst)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
