using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter.Tools
{
    public static class AstExtensions
    {
        private static readonly string[] s_scopePrefixes = { "Global:", "Local:", "Script:", "Private:" };

        public static IScriptExtent? GetFunctionNameExtent(this FunctionDefinitionAst functionDefinitionAst, IReadOnlyList<Token> tokens)
        {
            foreach (Token token in tokens)
            {
                if (functionDefinitionAst.Extent.Contains(token.Extent)
                    && token.Text.Equals(functionDefinitionAst.Name))
                {
                    return token.Extent!;
                }
            }

            return null;
        }

        public static object? GetValue(this NamedAttributeArgumentAst namedAttributeArgumentAst)
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

        /// <summary>
        /// Strips the scope prefix (Global:, Local:, Script:, Private:, or drive:) from a variable name.
        /// </summary>
        public static string GetNameWithoutScope(this VariableExpressionAst variableExpressionAst)
        {
            int colonIndex = variableExpressionAst.VariablePath.UserPath.IndexOf(":");

            return colonIndex == -1
                ? variableExpressionAst.VariablePath.UserPath
                : variableExpressionAst.VariablePath.UserPath.Substring(colonIndex + 1);
        }

        /// <summary>
        /// Strips the scope prefix (Global:, Local:, Script:, Private:) from a function name.
        /// </summary>
        public static string? GetNameWithoutScope(this FunctionDefinitionAst functionDefinitionAst)
        {
            return StripScopePrefix(functionDefinitionAst.Name);
        }

        /// <summary>
        /// Returns true if the <see cref="ParamBlockAst"/> has a <see cref="CmdletBindingAttribute"/>.
        /// </summary>
        public static bool HasCmdletBinding(this ParamBlockAst? paramBlock)
        {
            return paramBlock?.Attributes != null
                && AstTools.TryGetCmdletBindingAttributeAst(paramBlock.Attributes, out _);
        }

        /// <summary>
        /// Returns true if the file path refers to a PowerShell module script (.psm1).
        /// </summary>
        public static bool IsModuleScript(string? filePath)
        {
            return !string.IsNullOrEmpty(filePath)
                && filePath!.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the file path refers to a PowerShell module manifest (.psd1).
        /// </summary>
        public static bool IsModuleManifest(string? filePath)
        {
            return !string.IsNullOrEmpty(filePath)
                && filePath!.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase);
        }

        internal static string? StripScopePrefix(string? name)
        {
            if (name == null)
            {
                return null;
            }

            foreach (string prefix in s_scopePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(prefix.Length);
                }
            }

            return name;
        }

        /// <summary>
        /// Collects function names from <c>Export-ModuleMember -Function ...</c> calls in the script.
        /// Returns null if no Export-ModuleMember calls are found (meaning no explicit export restriction).
        /// Returns an empty set if Export-ModuleMember is called but no functions are exported.
        /// </summary>
        public static HashSet<string>? GetExportedFunctionNames(Ast? ast)
        {
            if (ast == null)
            {
                return null;
            }

            IEnumerable<Ast> commandAsts = ast.FindAll(
                static testAst => testAst is CommandAst cmdAst
                    && string.Equals(cmdAst.GetCommandName(), "Export-ModuleMember", StringComparison.OrdinalIgnoreCase),
                searchNestedScriptBlocks: true);

            HashSet<string>? exportedNames = null;

            foreach (CommandAst cmdAst in commandAsts)
            {
                if (exportedNames == null)
                {
                    exportedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                bool seenNamedFunction = false;
                bool collectPositionalFunctions = true;

                for (int i = 1; i < cmdAst.CommandElements.Count; i++)
                {
                    CommandElementAst element = cmdAst.CommandElements[i];
                    if (element is CommandParameterAst parameterAst)
                    {
                        collectPositionalFunctions = false;
                        seenNamedFunction = parameterAst.ParameterName.StartsWith("Function", StringComparison.OrdinalIgnoreCase);
                        if (!seenNamedFunction)
                        {
                            continue;
                        }

                        if (parameterAst.Argument is not null)
                        {
                            CollectExportedFunctionNames(parameterAst.Argument, exportedNames);
                        }

                        continue;
                    }

                    if (seenNamedFunction || collectPositionalFunctions)
                    {
                        CollectExportedFunctionNames(element, exportedNames);
                    }
                }
            }

            return exportedNames;
        }

        private static void CollectExportedFunctionNames(CommandElementAst element, HashSet<string> exportedNames)
        {
            if (element is StringConstantExpressionAst stringExpression)
            {
                exportedNames.Add(stringExpression.Value);
                return;
            }

            if (element is ArrayLiteralAst arrayLiteral)
            {
                for (int i = 0; i < arrayLiteral.Elements.Count; i++)
                {
                    if (arrayLiteral.Elements[i] is StringConstantExpressionAst arrayElement)
                    {
                        exportedNames.Add(arrayElement.Value);
                    }
                }

                return;
            }

            if (element is ArrayExpressionAst arrayExpression
                && arrayExpression.SubExpression is not null)
            {
                IReadOnlyList<StatementAst> statements = arrayExpression.SubExpression.Statements;
                for (int i = 0; i < statements.Count; i++)
                {
                    if (statements[i] is PipelineAst pipelineAst
                        && pipelineAst.PipelineElements.Count == 1
                        && pipelineAst.PipelineElements[0] is CommandExpressionAst commandExpressionAst
                        && commandExpressionAst.Expression is StringConstantExpressionAst stringAst)
                    {
                        exportedNames.Add(stringAst.Value);
                    }
                }
            }
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
            if (ast == null)
            {
                throw new ArgumentNullException("TODO");
            }

            IEnumerable<Ast> scriptBlockAsts = ast.FindAll(static testAst => testAst is ScriptBlockAst, true);

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
