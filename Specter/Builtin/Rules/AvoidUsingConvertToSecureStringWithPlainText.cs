using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.CommandDatabase;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingConvertToSecureStringWithPlainText", typeof(Strings), nameof(Strings.AvoidUsingConvertToSecureStringWithPlainTextDescription), Severity = DiagnosticSeverity.Error)]
    internal class AvoidUsingConvertToSecureStringWithPlainText : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidUsingConvertToSecureStringWithPlainText(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast foundAst in ast.FindAll(static testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string commandName = cmdAst.GetCommandName();

                if (commandName is null || !_commandDb.IsCommandOrAlias(commandName, "ConvertTo-SecureString"))
                {
                    continue;
                }

                if (!HasAsPlainTextParameter(cmdAst))
                {
                    continue;
                }

                string message = string.IsNullOrWhiteSpace(scriptPath)
                    ? Strings.AvoidUsingConvertToSecureStringWithPlainTextErrorScriptDefinition
                    : string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingConvertToSecureStringWithPlainTextError, System.IO.Path.GetFileName(scriptPath));

                yield return CreateDiagnostic(message, cmdAst.Extent);
            }
        }

        private static bool HasAsPlainTextParameter(CommandAst cmdAst)
        {
            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is CommandParameterAst param
                    && string.Equals(param.ParameterName, "AsPlainText", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
