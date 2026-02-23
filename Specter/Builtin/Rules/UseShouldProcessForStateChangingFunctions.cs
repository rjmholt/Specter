using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
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
            IEnumerable<Ast> funcDefWithNoShouldProcessAttrAsts = ast.FindAll(IsStateChangingFunctionWithNoShouldProcessAttribute, true);            

            foreach (FunctionDefinitionAst funcDefAst in funcDefWithNoShouldProcessAttrAsts)
            {
                yield return new ScriptDiagnostic(
                    RuleInfo,
                    string.Format(CultureInfo.CurrentCulture, Strings.UseShouldProcessForStateChangingFunctionsError, funcDefAst.Name),
                    funcDefAst.GetFunctionNameExtent(tokens) ?? funcDefAst.Extent,
                    DiagnosticSeverity.Warning);
            }
                            
        }
        /// <summary>
        /// Checks if the ast defines a state changing function
        /// </summary>
        /// <param name="ast"></param>
        /// <returns>Returns true or false</returns>
        private bool IsStateChangingFunctionWithNoShouldProcessAttribute(Ast ast)
        {
            var funcDefAst = ast as FunctionDefinitionAst;
            // SupportsShouldProcess is not supported in workflows
            if (funcDefAst == null || funcDefAst.IsWorkflow)
            {
                return false;
            }

            return IsStateChangingFunctionName(funcDefAst.Name) 
                    && (funcDefAst.Body.ParamBlock == null
                        || funcDefAst.Body.ParamBlock.Attributes == null
                        || !HasShouldProcessTrue(funcDefAst.Body.ParamBlock.Attributes));
        }

        /// <summary>
        /// Checks if an attribute has SupportShouldProcess set to $true
        /// </summary>
        /// <param name="attributeAsts"></param>
        /// <returns>Returns true or false</returns>
        private bool HasShouldProcessTrue(IEnumerable<AttributeAst> attributeAsts)
        {
            return AstTools.TryGetShouldProcessAttributeArgumentAst(attributeAsts, out NamedAttributeArgumentAst? shouldProcessArgument)
                && shouldProcessArgument is not null
                && AstTools.IsTrue(shouldProcessArgument.GetValue());
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




