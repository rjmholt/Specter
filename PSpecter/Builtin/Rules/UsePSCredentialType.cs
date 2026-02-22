using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UsePSCredentialType", typeof(Strings), nameof(Strings.UsePSCredentialTypeDescription))]
    public class UsePSCredentialType : ScriptRule
    {
        public UsePSCredentialType(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            bool requiresTransformationAttribute = !IsPowerShell5OrGreater();

            var sbAst = ast as ScriptBlockAst;
            if (sbAst != null
                && sbAst.ScriptRequirements?.RequiredPSVersion != null
                && sbAst.ScriptRequirements.RequiredPSVersion.Major == 5
                && requiresTransformationAttribute)
            {
                yield break;
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcDef = (FunctionDefinitionAst)node;
                IEnumerable<ParameterAst>? parameters = null;

                if (funcDef.Parameters != null)
                {
                    parameters = funcDef.Parameters;
                }

                if (funcDef.Body.ParamBlock?.Parameters != null)
                {
                    parameters = funcDef.Body.ParamBlock.Parameters;
                }

                if (parameters != null)
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.UsePSCredentialTypeError, funcDef.Name);
                    foreach (ScriptDiagnostic diagnostic in GetViolations(parameters, errorMessage, requiresTransformationAttribute))
                    {
                        yield return diagnostic;
                    }
                }
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is ScriptBlockAst, searchNestedScriptBlocks: true))
            {
                var scriptBlock = (ScriptBlockAst)node;

                if (scriptBlock.Parent is FunctionDefinitionAst)
                {
                    continue;
                }

                if (scriptBlock.ParamBlock?.Parameters != null)
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.UsePSCredentialTypeErrorSB);
                    foreach (ScriptDiagnostic diagnostic in GetViolations(scriptBlock.ParamBlock.Parameters, errorMessage, requiresTransformationAttribute))
                    {
                        yield return diagnostic;
                    }
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> GetViolations(
            IEnumerable<ParameterAst> parameters,
            string errorMessage,
            bool requiresTransformationAttribute)
        {
            foreach (ParameterAst parameter in parameters)
            {
                if (HasIncorrectCredentialUsage(parameter, requiresTransformationAttribute))
                {
                    yield return CreateDiagnostic(errorMessage, parameter.Extent);
                }
            }
        }

        private static bool HasIncorrectCredentialUsage(ParameterAst parameter, bool requiresTransformationAttribute)
        {
            if (!parameter.Name.VariablePath.UserPath.Equals("Credential", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            AttributeBaseAst? psCredentialType = parameter.Attributes.FirstOrDefault(attr =>
                (attr.TypeName is ArrayTypeName arrayType && arrayType.ElementType.GetReflectionType() == typeof(PSCredential))
                || attr.TypeName.GetReflectionType() == typeof(PSCredential));

            if (psCredentialType == null)
            {
                return true;
            }

            if (!requiresTransformationAttribute)
            {
                return false;
            }

            AttributeBaseAst? credentialAttribute = parameter.Attributes.FirstOrDefault(attr =>
                attr.TypeName.GetReflectionType() == typeof(CredentialAttribute)
                || attr.TypeName.FullName.Equals("System.Management.Automation.Credential", StringComparison.OrdinalIgnoreCase));

            if (psCredentialType != null
                && credentialAttribute != null
                && psCredentialType.Extent.EndOffset <= credentialAttribute.Extent.StartOffset)
            {
                return false;
            }

            return true;
        }

        private static bool IsPowerShell5OrGreater()
        {
#if CORECLR
            return true;
#else
            return false;
#endif
        }
    }
}
