using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    /// <summary>
    /// UseShouldProcessForStateChangingFunctions: Analyzes the ast to check if ShouldProcess is included in Advanced functions if the Verb of the function could change system state.
    /// </summary>
    [Rule("UseShouldProcessForStateChangingFunctions", typeof(Strings), nameof(Strings.UseShouldProcessForStateChangingFunctionsDescrption))]
    internal class UseShouldProcessForStateChangingFunctions : ScriptRule
    {
        private static readonly IReadOnlyList<string> s_stateChangingVerbs = new List<string>
        {
            { "New-" },
            { "Set-" },
            { "Remove-" },
            { "Start-" },
            { "Stop-" },
            { "Restart-" },
            { "Reset-" },
            { "Update-" }
        };

        internal UseShouldProcessForStateChangingFunctions(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check if ShouldProcess is included in Advanced functions if the Verb of the function could change system state.
        /// </summary>
        /// <param name="ast">The script's ast</param>
        /// <param name="scriptPath">The script's file path</param>
        /// <returns>A List of diagnostic results of this rule</returns>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            ScriptModel scriptModel = ScriptModel.GetOrCreate(ast, tokens, scriptPath);
            for (int i = 0; i < scriptModel.Functions.Count; i++)
            {
                FunctionSymbol function = scriptModel.Functions[i];
                FunctionDefinitionAst funcDefAst = function.Ast;
                if (funcDefAst.IsWorkflow || !function.IsCmdletStyle)
                {
                    continue;
                }

                if (!IsStateChangingFunctionName(function.Name) || function.SupportsShouldProcess)
                {
                    continue;
                }

                yield return new ScriptDiagnostic(
                    RuleInfo,
                    string.Format(CultureInfo.CurrentCulture, Strings.UseShouldProcessForStateChangingFunctionsError, funcDefAst.Name),
                    funcDefAst.GetFunctionNameExtent(tokens) ?? funcDefAst.Extent,
                    DiagnosticSeverity.Warning);
            }
        }

        private static bool IsStateChangingFunctionName(string functionName)
        {
            foreach (string verb in s_stateChangingVerbs)
            {
                if (functionName.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}




