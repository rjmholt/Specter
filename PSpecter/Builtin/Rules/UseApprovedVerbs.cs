using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// UseApprovedVerbs: Analyzes scripts to check that all defined functions use approved verbs.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseApprovedVerbs", typeof(Strings), nameof(Strings.UseApprovedVerbsDescription))]
    public class UseApprovedVerbs : ScriptRule
    {
        public UseApprovedVerbs(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            IEnumerable<Ast> funcAsts = ast.FindAll(testAst => testAst is FunctionDefinitionAst, true);
            if (funcAsts == null)
            {
                yield break;
            }

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                string funcName = funcAst.GetNameWithoutScope();
                if (funcName == null || !funcName.Contains("-"))
                {
                    continue;
                }

                string verb = funcName.Split('-')[0];

                if (!PowerShellConstants.ApprovedVerbs.Contains(verb))
                {
                    IScriptExtent nameExtent = funcAst.GetFunctionNameExtent(tokens) ?? funcAst.Extent;
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.UseApprovedVerbsError, funcName),
                        nameExtent);
                }
            }
        }
    }
}
