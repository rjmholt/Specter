using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidDefaultValueSwitchParameter", typeof(Strings), nameof(Strings.AvoidDefaultValueSwitchParameterDescription))]
    internal class AvoidDefaultTrueValueSwitchParameter : ScriptRule
    {
        public AvoidDefaultTrueValueSwitchParameter(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast foundAst in ast.FindAll(static testAst => testAst is ParameterAst, searchNestedScriptBlocks: true))
            {
                var paramAst = (ParameterAst)foundAst;

                if (paramAst.DefaultValue is null
                    || !string.Equals(paramAst.DefaultValue.Extent.Text, "$true", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsSwitchParameter(paramAst))
                {
                    continue;
                }

                string message = string.IsNullOrWhiteSpace(scriptPath)
                    ? Strings.AvoidDefaultValueSwitchParameterErrorScriptDefinition
                    : string.Format(CultureInfo.CurrentCulture, Strings.AvoidDefaultValueSwitchParameterError, System.IO.Path.GetFileName(scriptPath));

                yield return CreateDiagnostic(message, paramAst);
            }
        }

        private static bool IsSwitchParameter(ParameterAst paramAst)
        {
            foreach (AttributeBaseAst attr in paramAst.Attributes)
            {
                if (attr is TypeConstraintAst typeConstraint)
                {
                    Type? resolvedType = typeConstraint.TypeName.GetReflectionType();
                    if (resolvedType is not null && resolvedType == typeof(System.Management.Automation.SwitchParameter))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
