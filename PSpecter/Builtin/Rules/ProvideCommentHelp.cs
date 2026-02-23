using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using PSpecter.Configuration;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    internal class ProvideCommentHelpConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enabled: true);

        public bool ExportedOnly { get; set; } = true;

        public bool BlockComment { get; set; } = true;

        public string Placement { get; set; } = "before";

        public bool VSCodeSnippetCorrection { get; set; }
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("ProvideCommentHelp", typeof(Strings), nameof(Strings.ProvideCommentHelpDescription))]
    internal class ProvideCommentHelp : ConfigurableScriptRule<ProvideCommentHelpConfiguration>
    {
        public ProvideCommentHelp(RuleInfo ruleInfo, ProvideCommentHelpConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            HashSet<string>? exportedFunctions = Configuration.ExportedOnly
                ? GetExportedFunctionNames(ast)
                : null;

            foreach (Ast node in ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;

                if (funcAst.Parent is FunctionMemberAst)
                {
                    continue;
                }

                if (IsInsideTypeDefinition(funcAst))
                {
                    continue;
                }

                if (exportedFunctions is not null && !exportedFunctions.Contains(funcAst.Name))
                {
                    continue;
                }

                if (HasCommentHelp(funcAst, tokens))
                {
                    continue;
                }

                IScriptExtent nameExtent = GetFunctionNameExtent(funcAst, tokens);
                var corrections = BuildCorrection(funcAst, nameExtent);

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.ProvideCommentHelpError, funcAst.Name),
                    nameExtent,
                    corrections);
            }
        }

        private static bool IsInsideTypeDefinition(Ast ast)
        {
            Ast? parent = ast.Parent;
            while (parent is not null)
            {
                if (parent is TypeDefinitionAst)
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static bool HasCommentHelp(FunctionDefinitionAst funcAst, IReadOnlyList<Token> tokens)
        {
            if (funcAst.GetHelpContent() is not null)
            {
                return true;
            }

            return false;
        }

        private static IScriptExtent GetFunctionNameExtent(FunctionDefinitionAst funcAst, IReadOnlyList<Token> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.Extent.StartOffset >= funcAst.Extent.StartOffset
                    && t.Extent.EndOffset <= funcAst.Extent.EndOffset
                    && (t.Kind == TokenKind.Identifier || t.Kind == TokenKind.Generic)
                    && string.Equals(t.Text, funcAst.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return t.Extent;
                }
            }

            return funcAst.Extent;
        }

        private static HashSet<string> GetExportedFunctionNames(Ast ast)
        {
            var exported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Ast node in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: false))
            {
                var cmdAst = (CommandAst)node;
                string? commandName = cmdAst.GetCommandName();
                if (commandName is null || !string.Equals(commandName, "Export-ModuleMember", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool foundFunctionParam = false;
                bool pastCommandName = false;
                foreach (CommandElementAst element in cmdAst.CommandElements)
                {
                    if (!pastCommandName)
                    {
                        pastCommandName = true;
                        continue;
                    }

                    if (element is CommandParameterAst paramAst)
                    {
                        foundFunctionParam = paramAst.ParameterName.StartsWith("Function", StringComparison.OrdinalIgnoreCase);
                        if (foundFunctionParam && paramAst.Argument is StringConstantExpressionAst argStr)
                        {
                            exported.Add(argStr.Value);
                        }

                        continue;
                    }

                    CollectStringNames(element, exported);
                }
            }

            return exported;
        }

        private static void CollectStringNames(Ast element, HashSet<string> names)
        {
            if (element is StringConstantExpressionAst strConst)
            {
                names.Add(strConst.Value);
            }
            else if (element is ArrayLiteralAst arrayLit)
            {
                foreach (ExpressionAst elem in arrayLit.Elements)
                {
                    if (elem is StringConstantExpressionAst elemStr)
                    {
                        names.Add(elemStr.Value);
                    }
                }
            }
        }

        private IReadOnlyList<Correction> BuildCorrection(FunctionDefinitionAst funcAst, IScriptExtent nameExtent)
        {
            var parameters = GetParameters(funcAst);
            string helpText = BuildHelpText(parameters, funcAst);
            IScriptExtent correctionExtent = GetCorrectionExtent(funcAst, nameExtent);
            return new[] { new Correction(correctionExtent, helpText, "Add comment-based help") };
        }

        private string BuildHelpText(IReadOnlyList<string> parameterNames, FunctionDefinitionAst funcAst)
        {
            bool useBlock = Configuration.BlockComment;
            bool useSnippets = Configuration.VSCodeSnippetCorrection;
            string placement = Configuration.Placement ?? "before";
            int indent = funcAst.Extent.StartColumnNumber - 1;
            string indentStr = indent > 0 ? new string(' ', indent) : string.Empty;

            var sb = new StringBuilder();
            int snippetIndex = 1;

            if (string.Equals(placement, "begin", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("{");
                sb.Append(Environment.NewLine);
            }
            else if (string.Equals(placement, "end", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(Environment.NewLine);
            }

            if (useBlock)
            {
                sb.Append(indentStr).Append("<#");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(".SYNOPSIS");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(useSnippets ? "${" + snippetIndex++ + ":Short description}" : "Short description");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr);
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(".DESCRIPTION");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(useSnippets ? "${" + snippetIndex++ + ":Long description}" : "Long description");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr);
                sb.Append(Environment.NewLine);

                foreach (string paramName in parameterNames)
                {
                    sb.Append(indentStr).Append(".PARAMETER ").Append(paramName);
                    sb.Append(Environment.NewLine);
                    sb.Append(indentStr).Append(useSnippets ? "${" + snippetIndex++ + ":Parameter description}" : "Parameter description");
                    sb.Append(Environment.NewLine);
                    sb.Append(indentStr);
                    sb.Append(Environment.NewLine);
                }

                sb.Append(indentStr).Append(".EXAMPLE");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(useSnippets ? "${" + snippetIndex++ + ":An example}" : "An example");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr);
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(".NOTES");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append(useSnippets ? "${" + snippetIndex++ + ":General notes}" : "General notes");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#>");
            }
            else
            {
                sb.Append(indentStr).Append("##############################");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#.SYNOPSIS");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#").Append(useSnippets ? "${" + snippetIndex++ + ":Short description}" : "Short description");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#.DESCRIPTION");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#").Append(useSnippets ? "${" + snippetIndex++ + ":Long description}" : "Long description");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#");
                sb.Append(Environment.NewLine);

                foreach (string paramName in parameterNames)
                {
                    sb.Append(indentStr).Append("#.PARAMETER ").Append(paramName);
                    sb.Append(Environment.NewLine);
                    sb.Append(indentStr).Append("#").Append(useSnippets ? "${" + snippetIndex++ + ":Parameter description}" : "Parameter description");
                    sb.Append(Environment.NewLine);
                    sb.Append(indentStr).Append("#");
                    sb.Append(Environment.NewLine);
                }

                sb.Append(indentStr).Append("#.EXAMPLE");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#").Append(useSnippets ? "${" + snippetIndex++ + ":An example}" : "An example");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#.NOTES");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("#").Append(useSnippets ? "${" + snippetIndex++ + ":General notes}" : "General notes");
                sb.Append(Environment.NewLine);
                sb.Append(indentStr).Append("##############################");
            }

            sb.Append(Environment.NewLine);

            return sb.ToString();
        }

        private IScriptExtent GetCorrectionExtent(FunctionDefinitionAst funcAst, IScriptExtent nameExtent)
        {
            string placement = Configuration.Placement ?? "before";

            if (string.Equals(placement, "begin", StringComparison.OrdinalIgnoreCase))
            {
                IScriptExtent bodyExtent = funcAst.Body.Extent;
                return new MissingModuleManifestField.ScriptExtent(
                    new MissingModuleManifestField.ScriptPosition(bodyExtent.File!, bodyExtent.StartLineNumber, bodyExtent.StartColumnNumber),
                    new MissingModuleManifestField.ScriptPosition(bodyExtent.File!, bodyExtent.StartLineNumber, bodyExtent.StartColumnNumber + 1));
            }

            if (string.Equals(placement, "end", StringComparison.OrdinalIgnoreCase))
            {
                IScriptExtent bodyExtent = funcAst.Body.Extent;
                return new MissingModuleManifestField.ScriptExtent(
                    new MissingModuleManifestField.ScriptPosition(bodyExtent.File!, bodyExtent.EndLineNumber, bodyExtent.EndColumnNumber - 1),
                    new MissingModuleManifestField.ScriptPosition(bodyExtent.File!, bodyExtent.EndLineNumber, bodyExtent.EndColumnNumber - 1));
            }

            return new MissingModuleManifestField.ScriptExtent(
                new MissingModuleManifestField.ScriptPosition(funcAst.Extent.File!, funcAst.Extent.StartLineNumber, funcAst.Extent.StartColumnNumber),
                new MissingModuleManifestField.ScriptPosition(funcAst.Extent.File!, funcAst.Extent.StartLineNumber, funcAst.Extent.StartColumnNumber));
        }

        private static IReadOnlyList<string> GetParameters(FunctionDefinitionAst funcAst)
        {
            var result = new List<string>();

            IReadOnlyList<ParameterAst>? parameters = null;
            if (funcAst.Parameters is not null && funcAst.Parameters.Count > 0)
            {
                parameters = funcAst.Parameters;
            }
            else if (funcAst.Body?.ParamBlock?.Parameters is not null)
            {
                parameters = funcAst.Body.ParamBlock.Parameters;
            }

            if (parameters is not null)
            {
                foreach (ParameterAst param in parameters)
                {
                    result.Add(param.Name.VariablePath.UserPath);
                }
            }

            return result;
        }
    }
}
