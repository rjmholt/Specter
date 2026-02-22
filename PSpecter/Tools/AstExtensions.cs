using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter.Tools
{
    public static class AstExtensions
    {
        public static IScriptExtent GetFunctionNameExtent(this FunctionDefinitionAst functionDefinitionAst, IReadOnlyList<Token> tokens)
        {
            foreach (Token token in tokens)
            {
                if (functionDefinitionAst.Extent.Contains(token.Extent)
                    && token.Text.Equals(functionDefinitionAst.Name))
                {
                    return token.Extent;
                }
            }

            return null;
        }

        public static object GetValue(this NamedAttributeArgumentAst namedAttributeArgumentAst)
        {
            if (namedAttributeArgumentAst.ExpressionOmitted)
            {
                return true;
            }

            return AstTools.GetSafeValueFromAst(namedAttributeArgumentAst.Argument);
        }

        public static bool IsEnvironmentVariable(this VariableExpressionAst variableExpressionAst)
        {
            return string.Equals(variableExpressionAst.VariablePath.DriveName, "env", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetNameWithoutScope(this VariableExpressionAst variableExpressionAst)
        {
            int colonIndex = variableExpressionAst.VariablePath.UserPath.IndexOf(":");

            return colonIndex == -1
                ? variableExpressionAst.VariablePath.UserPath
                : variableExpressionAst.VariablePath.UserPath.Substring(colonIndex + 1);
        }

        public static bool IsSpecialVariable(this VariableExpressionAst variableExpressionAst)
        {
            string variableName = variableExpressionAst.VariablePath.UserPath;
            if (variableExpressionAst.VariablePath.IsGlobal
                || variableExpressionAst.VariablePath.IsScript
                || variableExpressionAst.VariablePath.IsLocal)
            {
                variableName = variableExpressionAst.GetNameWithoutScope();
            }

            return SpecialVariables.IsSpecialVariable(variableName);
        }

        /// <summary>
        /// GetPSMajorVersion: Retrieves Major PowerShell Version when supplied using #requires keyword in the script
        /// </summary>
        /// <returns>The name of this rule</returns>
        public static int GetPSRequiredVersionMajor(this Ast ast)
        {
            if (ast == null) throw new ArgumentNullException("TODO");

            IEnumerable<Ast> scriptBlockAsts = ast.FindAll(testAst => testAst is ScriptBlockAst, true);

            foreach (ScriptBlockAst scriptBlockAst in scriptBlockAsts)
            {
                if (null != scriptBlockAst.ScriptRequirements && null != scriptBlockAst.ScriptRequirements.RequiredPSVersion)
                {
                    return scriptBlockAst.ScriptRequirements.RequiredPSVersion.Major;
                }
            }

            // return a non valid Major version if #requires -Version is not supplied in the Script
            return -1;
        }
    }
}
