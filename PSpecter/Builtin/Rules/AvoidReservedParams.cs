// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidReservedParams: Analyzes the ast to check for reserved parameters in function definitions.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("ReservedParams", typeof(Strings), nameof(Strings.ReservedParamsDescription))]
    public class AvoidReservedParams : ScriptRule
    {
        public AvoidReservedParams(RuleInfo ruleInfo)
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

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Body?.ParamBlock == null
                    || funcAst.Body.ParamBlock.Parameters == null
                    || !funcAst.Body.ParamBlock.HasCmdletBinding())
                {
                    continue;
                }

                foreach (ParameterAst paramAst in funcAst.Body.ParamBlock.Parameters)
                {
                    string paramName = paramAst.Name.VariablePath.UserPath;
                    if (PowerShellConstants.CommonParameterNames.Contains(paramName))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.ReservedParamsError, funcAst.Name, paramName),
                            paramAst,
                            DiagnosticSeverity.Error);
                    }
                }
            }
        }
    }
}
