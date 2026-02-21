// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Tools;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingInvokeExpression", typeof(Strings), nameof(Strings.AvoidUsingInvokeExpressionRuleDescription))]
    public class AvoidUsingInvokeExpression : ScriptRule
    {
        private static readonly HashSet<string> s_invokeExpressionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Invoke-Expression",
            "iex",
        };

        public AvoidUsingInvokeExpression(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null) throw new ArgumentNullException(nameof(ast));

            foreach (Ast foundAst in ast.FindAll(testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string commandName = cmdAst.GetCommandName();

                if (commandName is null || !s_invokeExpressionNames.Contains(commandName))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingInvokeExpressionError),
                    cmdAst.Extent);
            }
        }
    }
}
