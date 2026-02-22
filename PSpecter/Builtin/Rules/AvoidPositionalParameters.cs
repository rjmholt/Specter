// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using PSpecter.Rules;
using PSpecter;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidPositionalParameters: Check to make sure that positional parameters are not used.
    /// </summary>
    public class AvoidPositionalParameters : ScriptRule
    {
        public AvoidPositionalParameters(RuleInfo ruleInfo) : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the ast to check that positional parameters are not used.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            // Find all function definitions in the script and add them to the set.
            IEnumerable<Ast> functionDefinitionAsts = ast.FindAll(testAst => testAst is FunctionDefinitionAst, true);
            var declaredFunctionNames = new HashSet<string>();

            foreach (FunctionDefinitionAst functionDefinitionAst in functionDefinitionAsts)
            {
                if (string.IsNullOrEmpty(functionDefinitionAst.Name))
                {
                    continue;
                }
                declaredFunctionNames.Add(functionDefinitionAst.Name);
            }

            // Finds all CommandAsts.
            IEnumerable<Ast> foundAsts = ast.FindAll(testAst => testAst is CommandAst, true);

            // Iterates all CommandAsts and check the command name.
            foreach (Ast foundAst in foundAsts)
            {
                CommandAst cmdAst = (CommandAst)foundAst;
                // Handles the exception caused by commands like, {& $PLINK $args 2> $TempErrorFile}.
                // You can also review the remark section in following document,
                // MSDN: CommandAst.GetCommandName Method
                if (cmdAst.GetCommandName() == null) continue;

                // TODO: Requires command database / Helper infrastructure to detect positional parameter usage
            }

            yield break;
        }
    }
}

