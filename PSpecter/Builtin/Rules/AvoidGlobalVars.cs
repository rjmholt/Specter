using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using PSpecter.Rules;
using PSpecter;
using PSpecter.Builtin.Rules;
using PSpecter.Tools;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidGlobalVars", typeof(Strings), nameof(Strings.AvoidGlobalVarsDescription))]
    internal class AvoidGlobalVars : ScriptRule
    {
        public AvoidGlobalVars(RuleInfo ruleInfo) : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            IEnumerable<Ast> varAsts = ast.FindAll(static testAst => testAst is VariableExpressionAst, true);

            if (varAsts == null)
            {
                yield break;
            }

            foreach (VariableExpressionAst varAst in varAsts)
            {
                if (varAst.VariablePath.IsGlobal)
                {
                    string variableName = varAst.GetNameWithoutScope();
                    if (SpecialVariables.IsSpecialVariable(variableName))
                    {
                        continue;
                    }

                    yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.AvoidGlobalVarsError, varAst.VariablePath.UserPath),
                            varAst);
                }
            }
        }
    }
}




