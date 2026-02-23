using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidUsingWMICmdlet: Avoid Using Get-WMIObject, Remove-WMIObject, Invoke-WmiMethod, Register-WmiEvent, Set-WmiInstance
    /// </summary>
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingWMICmdlet", typeof(Strings), nameof(Strings.AvoidUsingWMICmdletDescription))]
    internal class AvoidUsingWMICmdlet : ScriptRule
    {
        private static readonly HashSet<string> s_wmiCmdlets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Get-WmiObject",
            "Remove-WmiObject",
            "Invoke-WmiMethod",
            "Register-WmiEvent",
            "Set-WmiInstance",
        };

        public AvoidUsingWMICmdlet(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Avoid Using Get-WMIObject, Remove-WMIObject, Invoke-WmiMethod, Register-WmiEvent, Set-WmiInstance
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            // Rule is applicable only when PowerShell Version is > 3.0, since CIM cmdlet was introduced in 3.0
            int majorPSVersion = ast.GetPSRequiredVersionMajor();
            if (majorPSVersion > 0 && majorPSVersion < 3)
            {
                yield break;
            }

            // Finds all CommandAsts.
            IEnumerable<Ast> commandAsts = ast.FindAll(static testAst => testAst is CommandAst, true);

            // Iterate all CommandAsts and check the command name
            foreach (CommandAst cmdAst in commandAsts)
            {
                string commandName = cmdAst.GetCommandName();
                if (commandName == null || !s_wmiCmdlets.Contains(commandName))
                {
                    continue;
                }

                if (String.IsNullOrWhiteSpace(scriptPath))
                {
                    yield return CreateDiagnostic(
                        String.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingWMICmdletErrorScriptDefinition),
                        cmdAst.Extent);
                }
                else
                {
                    yield return CreateDiagnostic(
                        String.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingWMICmdletError, System.IO.Path.GetFileName(scriptPath)),
                        cmdAst.Extent);
                }
            }
        }
    }
}




