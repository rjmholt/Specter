using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingComputerNameHardcoded", typeof(Strings), nameof(Strings.AvoidComputerNameHardcodedDescription), Severity = DiagnosticSeverity.Error)]
    internal class AvoidUsingComputerNameHardcoded : ScriptRule
    {
        private static readonly HashSet<string> s_localhostRepresentations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            ".",
            "::1",
            "127.0.0.1",
        };

        public AvoidUsingComputerNameHardcoded(RuleInfo ruleInfo)
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

                    if (!string.Equals(paramAst.ParameterName, "ComputerName", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Ast computerNameArg = paramAst.Argument;
                    if (computerNameArg == null && i + 1 < cmdAst.CommandElements.Count)
                    {
                        computerNameArg = cmdAst.CommandElements[i + 1];
                    }

                    if (computerNameArg == null)
                    {
                        continue;
                    }

                    if (computerNameArg is ConstantExpressionAst constExpr
                        && constExpr.Value is string value
                        && !s_localhostRepresentations.Contains(value))
                    {
                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.AvoidComputerNameHardcodedError,
                                cmdAst.GetCommandName()),
                            cmdAst.Extent);
                    }
                }
            }
        }
    }
}
