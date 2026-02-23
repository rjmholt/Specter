using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidCommandInjection", typeof(Strings), nameof(Strings.AvoidCommandInjectionDescription))]
    internal class AvoidCommandInjection : ScriptRule
    {
        private static readonly HashSet<string> s_shellCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cmd", "cmd.exe",
            "powershell", "powershell.exe",
            "pwsh", "pwsh.exe",
            "bash", "sh",
        };

        private static readonly HashSet<string> s_shellSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/c", "/k", "-Command", "-c",
        };

        internal AvoidCommandInjection(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)found;
                string? commandName = cmdAst.GetCommandName();

                if (commandName is null || !s_shellCommands.Contains(commandName))
                {
                    continue;
                }

                var elements = cmdAst.CommandElements;
                for (int i = 1; i < elements.Count - 1; i++)
                {
                    if (!IsShellSwitch(elements[i]))
                    {
                        continue;
                    }

                    var argAst = elements[i + 1];
                    if (argAst is ExpandableStringExpressionAst)
                    {
                        yield return CreateDiagnostic(
                            string.Format(Strings.AvoidCommandInjectionError, commandName),
                            argAst.Extent);
                    }

                    break;
                }
            }
        }

        private static bool IsShellSwitch(CommandElementAst element)
        {
            if (element is CommandParameterAst param)
            {
                return s_shellSwitches.Contains("-" + param.ParameterName)
                    || s_shellSwitches.Contains("/" + param.ParameterName);
            }

            if (element is StringConstantExpressionAst constant)
            {
                return s_shellSwitches.Contains(constant.Value);
            }

            return false;
        }
    }
}
