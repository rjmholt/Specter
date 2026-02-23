using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
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

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static testAst => testAst is ParamBlockAst, searchNestedScriptBlocks: true))
            {
                var paramBlock = (ParamBlockAst)found;
                if (paramBlock.Parameters is null || paramBlock.Parameters.Count == 0)
                {
                    continue;
                }

                foreach (ParameterAst paramAst in paramBlock.Parameters)
                {
                    if (paramAst.DefaultValue is null)
                    {
                        continue;
                    }

                    if (!IsMandatoryInAllParameterSets(paramAst))
                    {
                        continue;
                    }

                    var diagnostic = CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidDefaultValueForMandatoryParameterError, paramAst.Name.VariablePath.UserPath),
                        paramAst);
                    diagnostic.RuleSuppressionId = paramAst.Name.VariablePath.UserPath;
                    yield return diagnostic;
                }
            }
        }

        /// <summary>
        /// Returns true only when every [Parameter] attribute on the parameter
        /// includes Mandatory=$true. A parameter with multiple [Parameter]
        /// attributes (one per parameter set) must be mandatory in ALL sets.
        /// </summary>
        private static bool IsMandatoryInAllParameterSets(ParameterAst paramAst)
        {
            bool foundAny = false;

            foreach (AttributeBaseAst attrBase in paramAst.Attributes)
            {
                if (attrBase is not AttributeAst attr
                    || !string.Equals(attr.TypeName.Name, "Parameter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foundAny = true;

                if (!HasMandatoryArgument(attr))
                {
                    return false;
                }
            }

            return foundAny;
        }

        private static bool HasMandatoryArgument(AttributeAst attr)
        {
            foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
            {
                if (string.Equals(namedArg.ArgumentName, "Mandatory", StringComparison.OrdinalIgnoreCase)
                    && AstTools.IsTrue(namedArg.GetValue()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
