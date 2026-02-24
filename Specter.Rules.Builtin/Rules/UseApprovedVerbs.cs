using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    /// <summary>
    /// UseApprovedVerbs: Analyzes scripts to check that all defined functions use approved verbs.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseApprovedVerbs", typeof(Strings), nameof(Strings.UseApprovedVerbsDescription))]
    internal class UseApprovedVerbs : ScriptRule
    {
        internal UseApprovedVerbs(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            ScriptModel scriptModel = ScriptModel.GetOrCreate(ast, tokens, scriptPath);
            for (int i = 0; i < scriptModel.Functions.Count; i++)
            {
                FunctionSymbol function = scriptModel.Functions[i];
                if (!function.IsCmdletStyle)
                {
                    continue;
                }

                string? funcName = function.Name;
                if (funcName == null || !funcName.Contains("-"))
                {
                    continue;
                }

                string verb = funcName.Split('-')[0];

                if (!PowerShellConstants.ApprovedVerbs.Contains(verb))
                {
                    IScriptExtent nameExtent = function.Ast.GetFunctionNameExtent(tokens) ?? function.Ast.Extent;
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.UseApprovedVerbsError, funcName),
                        nameExtent);
                }
            }
        }
    }
}
