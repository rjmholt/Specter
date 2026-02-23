using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidShouldContinueWithoutForce: Check that functions using $PSCmdlet.ShouldContinue() have a boolean Force parameter.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidShouldContinueWithoutForce", typeof(Strings), nameof(Strings.AvoidShouldContinueWithoutForceDescription))]
    internal class AvoidShouldContinueWithoutForce : ScriptRule
    {
        internal AvoidShouldContinueWithoutForce(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check that functions using ShouldContinue have a Force parameter.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> funcAsts = ast.FindAll(static testAst => testAst is FunctionDefinitionAst, true);

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                if (HasForceParameter(funcAst))
                {
                    continue;
                }

                IEnumerable<Ast> imeAsts = funcAst.FindAll(static testAst => testAst is InvokeMemberExpressionAst, true);

                foreach (InvokeMemberExpressionAst imeAst in imeAsts)
                {
                    if (imeAst.Expression is not VariableExpressionAst varExpr)
                    {
                        continue;
                    }

                    if (!string.Equals(varExpr.VariablePath.UserPath, "pscmdlet", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.Equals(imeAst.Member.Extent.Text, "shouldcontinue", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string message = string.IsNullOrWhiteSpace(scriptPath)
                        ? string.Format(CultureInfo.CurrentCulture, Strings.AvoidShouldContinueWithoutForceErrorScriptDefinition, funcAst.Name)
                        : string.Format(CultureInfo.CurrentCulture, Strings.AvoidShouldContinueWithoutForceError, funcAst.Name, System.IO.Path.GetFileName(scriptPath));

                    yield return CreateDiagnostic(message, imeAst);
                }
            }
        }

        private static bool HasForceParameter(FunctionDefinitionAst funcAst)
        {
            IEnumerable<Ast> paramAsts = funcAst.FindAll(static testAst => testAst is ParameterAst, true);

            foreach (ParameterAst paramAst in paramAsts)
            {
                if (!string.Equals(paramAst.Name.VariablePath.UserPath, "force", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? typeName = paramAst.StaticType?.FullName;

                if (typeName != null
                    && (string.Equals(typeName, "System.Boolean", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(typeName, "System.Management.Automation.SwitchParameter", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
