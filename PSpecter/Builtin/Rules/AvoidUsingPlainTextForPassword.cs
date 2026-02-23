using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingPlainTextForPassword", typeof(Strings), nameof(Strings.AvoidUsingPlainTextForPasswordDescription))]
    internal class AvoidUsingPlainTextForPassword : ScriptRule
    {
        private static readonly string[] s_passwordNames = { "Password", "Passphrase", "Cred", "Credential" };

        internal AvoidUsingPlainTextForPassword(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is ParameterAst, searchNestedScriptBlocks: true))
            {
                var paramAst = (ParameterAst)node;
                Type? paramType = paramAst.StaticType;
                string paramName = paramAst.Name.VariablePath.ToString();

                if (!IsPasswordRelated(paramName))
                {
                    continue;
                }

                if (paramType is null || !IsPlainTextType(paramType))
                {
                    continue;
                }

                var diagnostic = CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingPlainTextForPasswordError, paramAst.Name),
                    paramAst.Extent,
                    GetCorrections(paramAst));
                diagnostic.RuleSuppressionId = paramAst.Name.VariablePath.UserPath;
                yield return diagnostic;
            }
        }

        private static bool IsPasswordRelated(string paramName)
        {
            foreach (string password in s_passwordNames)
            {
                if (paramName.IndexOf(password, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlainTextType(Type? paramType)
        {
            if (paramType is null)
            {
                return false;
            }

            if (paramType.IsArray)
            {
                Type? elementType = paramType.GetElementType();
                return elementType == typeof(string) || elementType == typeof(object);
            }

            return paramType == typeof(string) || paramType == typeof(object);
        }

        private static List<Correction> GetCorrections(ParameterAst paramAst)
        {
            var corrections = new List<Correction>();
            TypeConstraintAst? typeAttr = GetTypeConstraintAst(paramAst);

            foreach (string correctionType in new[] { "SecureString", "PSCredential" })
            {
                IScriptExtent extent;
                string correctionText;

                if (typeAttr == null)
                {
                    extent = paramAst.Name.Extent;
                    correctionText = $"[{correctionType}] {paramAst.Name.Extent.Text}";
                }
                else
                {
                    extent = typeAttr.Extent;
                    correctionText = typeAttr.TypeName.IsArray ? $"[{correctionType}[]]" : $"[{correctionType}]";
                }

                string description = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.AvoidUsingPlainTextForPasswordCorrectionDescription,
                    paramAst.Name.Extent.Text,
                    correctionType);

                corrections.Add(new Correction(extent, correctionText, description));
            }

            return corrections;
        }

        private static TypeConstraintAst? GetTypeConstraintAst(ParameterAst paramAst)
        {
            if (paramAst.Attributes != null)
            {
                foreach (AttributeBaseAst attr in paramAst.Attributes)
                {
                    if (attr is TypeConstraintAst typeConstraint)
                    {
                        return typeConstraint;
                    }
                }
            }

            return null;
        }
    }
}
