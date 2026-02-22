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
    /// AvoidReservedParams: Analyzes the ast to check for reserved parameters in function definitions.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("ReservedParams", typeof(Strings), nameof(Strings.ReservedParamsDescription))]
    public class AvoidReservedParams : ScriptRule
    {
        private static readonly IReadOnlyList<string> s_commonParameterNames = new[]
        {
            "Verbose",
            "Debug",
            "ErrorAction",
            "WarningAction",
            "InformationAction",
            "ErrorVariable",
            "WarningVariable",
            "InformationVariable",
            "OutVariable",
            "OutBuffer",
            "PipelineVariable",
        };

        public AvoidReservedParams(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check for reserved parameters in function definitions.
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

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (funcAst.Body?.ParamBlock == null
                    || funcAst.Body.ParamBlock.Attributes == null
                    || funcAst.Body.ParamBlock.Parameters == null)
                {
                    continue;
                }

                if (!funcAst.Body.ParamBlock.Attributes.Any(attr =>
                    attr.TypeName.GetReflectionType() == typeof(CmdletBindingAttribute)))
                {
                    continue;
                }

                foreach (ParameterAst paramAst in funcAst.Body.ParamBlock.Parameters)
                {
                    string paramName = paramAst.Name.VariablePath.UserPath;
                    if (s_commonParameterNames.Contains(paramName, StringComparer.OrdinalIgnoreCase))
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
