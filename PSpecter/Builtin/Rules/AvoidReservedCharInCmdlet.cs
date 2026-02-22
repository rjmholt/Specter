// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSpecter.Rules;

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

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that cmdlets do not use reserved characters.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> funcAsts = ast.FindAll(testAst => testAst is FunctionDefinitionAst, true);
            if (funcAsts == null)
            {
                yield break;
            }

            string reservedChars = Strings.ReserverCmdletChars;

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Body?.ParamBlock?.Attributes == null)
                {
                    continue;
                }

                if (!funcAst.Body.ParamBlock.Attributes.Any(attr =>
                    attr.TypeName.GetReflectionType() == typeof(CmdletBindingAttribute)))
                {
                    continue;
                }

                string funcName = FunctionNameWithoutScope(funcAst.Name);
                if (funcName != null && funcName.Intersect(reservedChars).Any())
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.ReservedCmdletCharError, funcAst.Name),
                        funcAst);
                }
            }
        }

        private static string FunctionNameWithoutScope(string name)
        {
            if (name == null)
            {
                return null;
            }

            string[] scopePrefixes = { "Global:", "Local:", "Script:", "Private:" };
            foreach (string prefix in scopePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(prefix.Length);
                }
            }

            return name;
        }
    }
}
