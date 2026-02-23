using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSpecter.Rules;
using SMA = System.Management.Automation.Language;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingUsernameAndPasswordParams", typeof(Strings), nameof(Strings.AvoidUsernameAndPasswordParamsDescription), Severity = DiagnosticSeverity.Error)]
    internal class AvoidUserNameAndPasswordParams : ScriptRule
    {
        private static readonly string[] s_passwordNames = { "Password", "Passphrase" };
        private static readonly string[] s_usernameNames = { "Username", "User" };

        private static readonly Type[] s_typeAllowList =
        {
            typeof(CredentialAttribute),
            typeof(PSCredential),
            typeof(System.Security.SecureString),
            typeof(SwitchParameter),
            typeof(bool),
        };

        public AvoidUserNameAndPasswordParams(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;
                IEnumerable<Ast> paramAsts = funcAst.FindAll(static testAst => testAst is ParameterAst, searchNestedScriptBlocks: true);

                ParameterAst? usernameAst = null;
                ParameterAst? passwordAst = null;

                foreach (ParameterAst paramAst in paramAsts)
                {
                    string paramName = paramAst.Name.VariablePath.ToString();

                    if (passwordAst == null && IsMatch(paramName, s_passwordNames) && !HasAllowListAttribute(paramAst))
                    {
                        passwordAst = paramAst;
                    }

                    if (usernameAst == null && IsMatch(paramName, s_usernameNames))
                    {
                        usernameAst = paramAst;
                    }
                }

                if (usernameAst != null && passwordAst != null)
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsernameAndPasswordParamsError, funcAst.Name),
                        GetCombinedExtent(usernameAst, passwordAst));
                }
            }
        }

        private static bool IsMatch(string paramName, string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (paramName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAllowListAttribute(ParameterAst paramAst)
        {
            foreach (AttributeBaseAst attr in paramAst.Attributes)
            {
                foreach (Type allowedType in s_typeAllowList)
                {
                    if (IsAttributeOfType(attr, allowedType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsAttributeOfType(AttributeBaseAst attributeAst, Type type)
        {
            if (attributeAst.TypeName is ArrayTypeName arrayType)
            {
                return arrayType.ElementType.GetReflectionType() == type;
            }

            return attributeAst.TypeName.GetReflectionType() == type;
        }

        private static IScriptExtent GetCombinedExtent(ParameterAst usernameAst, ParameterAst passwordAst)
        {
            var usrExt = usernameAst.Extent;
            var pwdExt = passwordAst.Extent;

            bool usrFirst = usrExt.StartLineNumber < pwdExt.StartLineNumber
                || (usrExt.StartLineNumber == pwdExt.StartLineNumber && usrExt.StartColumnNumber < pwdExt.StartColumnNumber);

            IScriptExtent startExt = usrFirst ? usrExt : pwdExt;
            IScriptExtent endExt = usrFirst ? pwdExt : usrExt;

            var startPos = new SMA.ScriptPosition(
                startExt.File,
                startExt.StartLineNumber,
                startExt.StartColumnNumber,
                startExt.StartScriptPosition.Line);
            var endPos = new SMA.ScriptPosition(
                endExt.File,
                endExt.EndLineNumber,
                endExt.EndColumnNumber,
                endExt.EndScriptPosition.Line);

            return new SMA.ScriptExtent(startPos, endPos);
        }
    }
}
