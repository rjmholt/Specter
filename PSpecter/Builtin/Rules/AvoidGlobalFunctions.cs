// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidGlobalFunctions: Check that functions are not declared with Global: prefix in module scripts.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidGlobalFunctions", typeof(Strings), nameof(Strings.AvoidGlobalFunctionsDescription))]
    public class AvoidGlobalFunctions : ScriptRule
    {
        public AvoidGlobalFunctions(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check that functions are not declared with Global: prefix in module scripts.
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

            IEnumerable<Ast> funcAsts = ast.FindAll(testAst => testAst is FunctionDefinitionAst, true);

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Name != null && funcAst.Name.StartsWith("Global:", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateDiagnostic(Strings.AvoidGlobalFunctionsError, funcAst);
                }
            }
        }
    }
}
