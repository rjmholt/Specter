using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidGlobalFunctions: Check that functions are not declared with Global: prefix in module scripts.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidGlobalFunctions", typeof(Strings), nameof(Strings.AvoidGlobalFunctionsDescription))]
    internal class AvoidGlobalFunctions : ScriptRule
    {
        public AvoidGlobalFunctions(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (!AstExtensions.IsModuleScript(scriptPath))
            {
                yield break;
            }

            IEnumerable<Ast> funcAsts = ast.FindAll(static testAst => testAst is FunctionDefinitionAst, true);

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Name != null && funcAst.Name.StartsWith("Global:", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateDiagnostic(Strings.AvoidGlobalFunctionsError, funcAst);
                }
            }
        }
    }
}
