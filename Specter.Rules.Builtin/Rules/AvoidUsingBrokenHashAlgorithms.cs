using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingBrokenHashAlgorithms", typeof(Strings), nameof(Strings.AvoidUsingBrokenHashAlgorithmsDescription))]
    internal class AvoidUsingBrokenHashAlgorithms : ScriptRule
    {
        private static readonly HashSet<string> s_brokenAlgorithms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MD5",
            "SHA1",
        };

        internal AvoidUsingBrokenHashAlgorithms(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;

                for (int i = 0; i < cmdAst.CommandElements.Count; i++)
                {
                    if (!(cmdAst.CommandElements[i] is CommandParameterAst paramAst))
                    {
                        continue;
                    }

                    if (!string.Equals(paramAst.ParameterName, "Algorithm", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Ast algorithmArg = paramAst.Argument;
                    if (algorithmArg == null && i + 1 < cmdAst.CommandElements.Count)
                    {
                        algorithmArg = cmdAst.CommandElements[i + 1];
                    }

                    if (algorithmArg == null)
                    {
                        continue;
                    }

                    if (algorithmArg is ConstantExpressionAst constExpr
                        && constExpr.Value is string algorithm
                        && s_brokenAlgorithms.Contains(algorithm))
                    {
                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.AvoidUsingBrokenHashAlgorithmsError,
                                cmdAst.GetCommandName(),
                                algorithm),
                            cmdAst.Extent);
                    }
                }
            }
        }
    }
}
