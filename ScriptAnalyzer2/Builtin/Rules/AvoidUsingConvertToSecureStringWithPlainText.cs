// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Runtime;
using Microsoft.PowerShell.ScriptAnalyzer.Tools;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingConvertToSecureStringWithPlainText", typeof(Strings), nameof(Strings.AvoidUsingConvertToSecureStringWithPlainTextDescription), Severity = DiagnosticSeverity.Error)]
    public class AvoidUsingConvertToSecureStringWithPlainText : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        public AvoidUsingConvertToSecureStringWithPlainText(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null) throw new ArgumentNullException(nameof(ast));

            foreach (Ast foundAst in ast.FindAll(testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
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

                string message = string.IsNullOrWhiteSpace(fileName)
                    ? Strings.AvoidUsingConvertToSecureStringWithPlainTextErrorScriptDefinition
                    : string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingConvertToSecureStringWithPlainTextError, System.IO.Path.GetFileName(fileName));

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
