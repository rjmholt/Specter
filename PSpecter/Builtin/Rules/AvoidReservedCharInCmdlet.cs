// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidReservedCharInCmdlet: Analyzes script to check for reserved characters in cmdlet names.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("ReservedCmdletChar", typeof(Strings), nameof(Strings.ReservedCmdletCharDescription))]
    public class AvoidReservedCharInCmdlet : ScriptRule
    {
        public AvoidReservedCharInCmdlet(RuleInfo ruleInfo)
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

            string reservedChars = Strings.ReserverCmdletChars;
            HashSet<string> exportedFunctions = AstExtensions.GetExportedFunctionNames(ast);

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Body?.ParamBlock == null || !funcAst.Body.ParamBlock.HasCmdletBinding())
                {
                    continue;
                }

                string funcName = funcAst.GetNameWithoutScope();

                // Only flag functions that are explicitly exported
                if (exportedFunctions != null
                    && !exportedFunctions.Contains(funcAst.Name)
                    && !exportedFunctions.Contains(funcName))
                {
                    continue;
                }

                // If no Export-ModuleMember is found, skip entirely (matches PSSA behavior)
                if (exportedFunctions == null)
                {
                    continue;
                }

                if (funcName != null && funcName.Intersect(reservedChars).Any())
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.ReservedCmdletCharError, funcAst.Name),
                        funcAst);
                }
            }
        }
    }
}
