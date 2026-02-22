using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseSupportsShouldProcess", typeof(Strings), nameof(Strings.UseSupportsShouldProcessDescription))]
    public class UseSupportsShouldProcess : ScriptRule
    {
        public UseSupportsShouldProcess(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;
                ParamBlockAst? paramBlockAst;
                ParameterAst[]? parameters = GetParameterAsts(funcAst, out paramBlockAst);
                if (parameters == null || parameters.Length == 0)
                {
                    continue;
                }

                int whatIfIndex = FindParameterIndex(parameters, "WhatIf");
                int confirmIndex = FindParameterIndex(parameters, "Confirm");

                if (whatIfIndex < 0 && confirmIndex < 0)
                {
                    continue;
                }

                IScriptExtent violationExtent = whatIfIndex >= 0
                    ? parameters[whatIfIndex].Extent
                    : parameters[confirmIndex].Extent;

                var corrections = BuildCorrections(
                    whatIfIndex, confirmIndex, parameters, paramBlockAst, funcAst, tokens);

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseSupportsShouldProcessError,
                        funcAst.Name),
                    violationExtent,
                    corrections);
            }
        }

        private static ParameterAst[]? GetParameterAsts(FunctionDefinitionAst funcAst, out ParamBlockAst? paramBlockAst)
        {
            if (funcAst.Parameters != null && funcAst.Parameters.Count > 0)
            {
                paramBlockAst = null;
                return funcAst.Parameters.ToArray();
            }

            if (funcAst.Body?.ParamBlock?.Parameters != null && funcAst.Body.ParamBlock.Parameters.Count > 0)
            {
                paramBlockAst = funcAst.Body.ParamBlock;
                return funcAst.Body.ParamBlock.Parameters.ToArray();
            }

            paramBlockAst = null;
            return null;
        }

        private static int FindParameterIndex(ParameterAst[] parameters, string name)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name.VariablePath.UserPath.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private IReadOnlyList<Correction> BuildCorrections(
            int whatIfIndex,
            int confirmIndex,
            ParameterAst[] parameters,
            ParamBlockAst? paramBlockAst,
            FunctionDefinitionAst funcAst,
            IReadOnlyList<Token> tokens)
        {
            string funcText = funcAst.Extent.Text;
            int funcStartOffset = funcAst.Extent.StartOffset;

            var edits = new List<TextEdit>();

            if (paramBlockAst != null)
            {
                CollectParamBlockEdits(edits, whatIfIndex, confirmIndex, parameters, paramBlockAst, funcStartOffset);
            }
            else
            {
                CollectFunctionParameterEdits(edits, whatIfIndex, confirmIndex, parameters!, funcAst, tokens, funcStartOffset);
            }

            edits.Sort((a, b) => b.StartOffset.CompareTo(a.StartOffset));

            var sb = new StringBuilder(funcText);
            foreach (TextEdit edit in edits)
            {
                sb.Remove(edit.StartOffset, edit.EndOffset - edit.StartOffset);
                sb.Insert(edit.StartOffset, edit.Replacement);
            }

            return new[]
            {
                new Correction(funcAst.Extent, sb.ToString(), "Add SupportsShouldProcess and remove manual WhatIf/Confirm")
            };
        }

        private static void CollectParamBlockEdits(
            List<TextEdit> edits,
            int whatIfIndex,
            int confirmIndex,
            ParameterAst[] parameters,
            ParamBlockAst paramBlockAst,
            int funcStartOffset)
        {
            if (whatIfIndex >= 0)
            {
                edits.Add(GetParamRemovalEdit(whatIfIndex, parameters, funcStartOffset));
            }

            if (confirmIndex >= 0)
            {
                edits.Add(GetParamRemovalEdit(confirmIndex, parameters, funcStartOffset));
            }

            AddCmdletBindingEdits(edits, paramBlockAst, funcStartOffset);
        }

        private static void CollectFunctionParameterEdits(
            List<TextEdit> edits,
            int whatIfIndex,
            int confirmIndex,
            ParameterAst[] parameters,
            FunctionDefinitionAst funcAst,
            IReadOnlyList<Token> tokens,
            int funcStartOffset)
        {
            Token[] funcTokens = tokens
                .Where(t => funcAst.Extent.StartOffset <= t.Extent.StartOffset
                    && t.Extent.EndOffset <= funcAst.Extent.EndOffset)
                .ToArray();

            Token? lParen = funcTokens.FirstOrDefault(t => t.Kind == TokenKind.LParen);
            Token? rParen = funcTokens.FirstOrDefault(t => t.Kind == TokenKind.RParen);

            if (lParen is null || rParen is null)
            {
                return;
            }

            Token? nameToken = funcTokens.LastOrDefault(
                t => t.Extent.EndOffset <= lParen.Extent.StartOffset && t.Kind != TokenKind.NewLine);

            int removeStart = nameToken is not null
                ? nameToken.Extent.EndOffset - funcStartOffset
                : lParen.Extent.StartOffset - funcStartOffset;

            int removeEnd = rParen.Extent.EndOffset - funcStartOffset;

            edits.Add(new TextEdit(removeStart, removeEnd, ""));

            int bodyStart = funcAst.Body.Extent.StartOffset - funcStartOffset;
            string indent = new string(' ', funcAst.Extent.StartScriptPosition.ColumnNumber + 3);

            var paramBlockLines = new List<string>();
            paramBlockLines.Add("{");
            paramBlockLines.Add(indent + "[CmdletBinding(SupportsShouldProcess)]");
            paramBlockLines.Add(indent + "param(");

            var usableParams = parameters
                .Where((_, i) => i != whatIfIndex && i != confirmIndex)
                .ToArray();

            for (int i = 0; i < usableParams.Length; i++)
            {
                string suffix = i < usableParams.Length - 1 ? "," : "";
                foreach (string line in usableParams[i].Extent.Text.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r');
                    paramBlockLines.Add(indent + "    " + trimmed + suffix);
                    suffix = "";
                }
            }

            paramBlockLines.Add(indent + ")");

            string replacement = string.Join(Environment.NewLine, paramBlockLines);
            edits.Add(new TextEdit(bodyStart, bodyStart + 1, replacement));
        }

        private static void AddCmdletBindingEdits(
            List<TextEdit> edits,
            ParamBlockAst paramBlockAst,
            int funcStartOffset)
        {
            if (paramBlockAst.Attributes == null
                || !AstTools.TryGetCmdletBindingAttributeAst(paramBlockAst.Attributes, out AttributeAst? cmdletBindingAttr)
                || cmdletBindingAttr is null)
            {
                int paramBlockStart = paramBlockAst.Extent.StartOffset - funcStartOffset;
                int lineStartOffset = paramBlockStart - (paramBlockAst.Extent.StartColumnNumber - 1);
                string indent = new string(' ', paramBlockAst.Extent.StartColumnNumber - 1);
                string insertion = indent + "[CmdletBinding(SupportsShouldProcess)]" + Environment.NewLine;
                edits.Add(new TextEdit(lineStartOffset, lineStartOffset, insertion));
                return;
            }

            if (AstTools.TryGetShouldProcessAttributeArgumentAst(paramBlockAst.Attributes, out NamedAttributeArgumentAst? shouldProcessArg)
                && shouldProcessArg is not null)
            {
                object? argValue = shouldProcessArg.GetValue();
                if (!AstTools.IsTrue(argValue))
                {
                    if (!shouldProcessArg.ExpressionOmitted)
                    {
                        int argStart = shouldProcessArg.Argument.Extent.StartOffset - funcStartOffset;
                        int argEnd = shouldProcessArg.Argument.Extent.EndOffset - funcStartOffset;
                        edits.Add(new TextEdit(argStart, argEnd, "$true"));
                    }
                }
                return;
            }

            string attrText = cmdletBindingAttr!.Extent.Text;
            int openParenIdx = attrText.IndexOf('(');
            if (openParenIdx < 0)
            {
                return;
            }

            int insertOffset = cmdletBindingAttr.Extent.StartOffset + openParenIdx + 1 - funcStartOffset;
            bool hasExistingArgs = cmdletBindingAttr.NamedArguments.Count > 0
                || cmdletBindingAttr.PositionalArguments.Count > 0;
            string insertText = hasExistingArgs ? "SupportsShouldProcess, " : "SupportsShouldProcess";
            edits.Add(new TextEdit(insertOffset, insertOffset, insertText));
        }

        private static TextEdit GetParamRemovalEdit(int paramIndex, ParameterAst[] parameters, int funcStartOffset)
        {
            IScriptExtent paramExtent = parameters[paramIndex].Extent;

            int startOffset, endOffset;

            if (paramIndex < parameters.Length - 1)
            {
                startOffset = paramExtent.StartOffset - funcStartOffset;
                endOffset = parameters[paramIndex + 1].Extent.StartOffset - funcStartOffset;
            }
            else
            {
                if (paramIndex > 0
                    && !IsWhatIfOrConfirm(parameters[paramIndex - 1]))
                {
                    startOffset = parameters[paramIndex - 1].Extent.EndOffset - funcStartOffset;
                }
                else
                {
                    startOffset = paramExtent.StartOffset - funcStartOffset;
                }

                endOffset = paramExtent.EndOffset - funcStartOffset;
            }

            return new TextEdit(startOffset, endOffset, "");
        }

        private static bool IsWhatIfOrConfirm(ParameterAst parameterAst)
        {
            string name = parameterAst.Name.VariablePath.UserPath;
            return name.Equals("WhatIf", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Confirm", StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct TextEdit
        {
            public readonly int StartOffset;
            public readonly int EndOffset;
            public readonly string Replacement;

            public TextEdit(int startOffset, int endOffset, string replacement)
            {
                StartOffset = startOffset;
                EndOffset = endOffset;
                Replacement = replacement;
            }
        }
    }
}
