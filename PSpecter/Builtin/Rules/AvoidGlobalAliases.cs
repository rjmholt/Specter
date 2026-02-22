// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidGlobalAliases: Check that New-Alias is not called with -Scope Global in module scripts.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidGlobalAliases", typeof(Strings), nameof(Strings.AvoidGlobalAliasesDescription))]
    public class AvoidGlobalAliases : ScriptRule
    {
        public AvoidGlobalAliases(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check that New-Alias is not called with -Scope Global in module scripts.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            IEnumerable<Ast> commandAsts = ast.FindAll(testAst => testAst is CommandAst, true);

            foreach (CommandAst commandAst in commandAsts)
            {
                string commandName = commandAst.GetCommandName();

                if (commandName == null)
                {
                    continue;
                }

                if (!string.Equals(commandName, "New-Alias", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(commandName, "nal", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parameterBindings = StaticParameterBinder.BindCommand(commandAst);

                if (!parameterBindings.BoundParameters.ContainsKey("Scope"))
                {
                    continue;
                }

                object scopeValue = parameterBindings.BoundParameters["Scope"].ConstantValue;

                if (scopeValue != null && string.Equals(scopeValue.ToString(), "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateDiagnostic(Strings.AvoidGlobalAliasesError, commandAst);
                }
            }
        }
    }
}
