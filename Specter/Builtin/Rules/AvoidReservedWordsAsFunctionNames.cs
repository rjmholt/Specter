using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidReservedWordsAsFunctionNames", typeof(Strings), nameof(Strings.AvoidReservedWordsAsFunctionNamesDescription))]
    internal class AvoidReservedWordsAsFunctionNames : ScriptRule
    {
        private static readonly HashSet<string> s_reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "begin", "break", "catch", "class", "configuration",
            "continue", "data", "define", "do",
            "dynamicparam", "else", "elseif", "end",
            "enum", "exit", "filter", "finally",
            "for", "foreach", "from", "function",
            "if", "parallel", "param", "process",
            "return", "sequence", "switch",
            "throw", "trap", "try", "type",
            "until", "using", "var", "while", "workflow"
        };

        internal AvoidReservedWordsAsFunctionNames(RuleInfo ruleInfo)
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

                if (funcAst.Parent is FunctionMemberAst)
                {
                    continue;
                }

                string? nameWithoutScope = funcAst.GetNameWithoutScope();
                if (string.IsNullOrEmpty(nameWithoutScope) || !s_reservedWords.Contains(nameWithoutScope))
                {
                    continue;
                }

                IScriptExtent nameExtent = GetFunctionNameExtent(funcAst, tokens);
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidReservedWordsAsFunctionNamesError, nameWithoutScope),
                    nameExtent);
            }
        }

        private static IScriptExtent GetFunctionNameExtent(FunctionDefinitionAst funcAst, IReadOnlyList<Token> tokens)
        {
            Token? firstMatch = null;
            Token? secondMatch = null;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.Extent.StartOffset < funcAst.Extent.StartOffset
                    || t.Extent.EndOffset > funcAst.Extent.EndOffset)
                {
                    continue;
                }

                if (string.Equals(t.Text, funcAst.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (firstMatch is null)
                    {
                        firstMatch = t;
                    }
                    else
                    {
                        secondMatch = t;
                        break;
                    }
                }
            }

            if (funcAst.Name.Equals("function", StringComparison.OrdinalIgnoreCase)
                || funcAst.Name.Equals("filter", StringComparison.OrdinalIgnoreCase)
                || funcAst.Name.Equals("workflow", StringComparison.OrdinalIgnoreCase)
                || funcAst.Name.Equals("configuration", StringComparison.OrdinalIgnoreCase))
            {
                return (secondMatch ?? firstMatch)?.Extent ?? funcAst.Extent;
            }

            return firstMatch?.Extent ?? funcAst.Extent;
        }
    }
}
