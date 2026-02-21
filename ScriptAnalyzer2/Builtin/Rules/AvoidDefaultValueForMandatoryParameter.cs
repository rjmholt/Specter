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
    [Rule("AvoidDefaultValueForMandatoryParameter", typeof(Strings), nameof(Strings.AvoidDefaultValueForMandatoryParameterDescription))]
    public class AvoidDefaultValueForMandatoryParameter : ScriptRule
    {
        public AvoidDefaultValueForMandatoryParameter(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null) throw new ArgumentNullException(nameof(ast));

            foreach (Ast foundAst in ast.FindAll(testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)foundAst;

                if (funcAst.Body?.ParamBlock?.Parameters is null)
                {
                    continue;
                }

                foreach (ParameterAst paramAst in funcAst.Body.ParamBlock.Parameters)
                {
                    if (paramAst.DefaultValue is null)
                    {
                        continue;
                    }

                    if (!IsMandatory(paramAst))
                    {
                        continue;
                    }

                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidDefaultValueForMandatoryParameterError, paramAst.Name.VariablePath.UserPath),
                        paramAst);
                }
            }
        }

        private static bool IsMandatory(ParameterAst paramAst)
        {
            foreach (AttributeBaseAst attrBase in paramAst.Attributes)
            {
                if (attrBase is not AttributeAst attr)
                {
                    continue;
                }

                if (!string.Equals(attr.TypeName.Name, "Parameter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
                {
                    if (string.Equals(namedArg.ArgumentName, "Mandatory", StringComparison.OrdinalIgnoreCase)
                        && AstTools.IsTrue(namedArg.GetValue()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
