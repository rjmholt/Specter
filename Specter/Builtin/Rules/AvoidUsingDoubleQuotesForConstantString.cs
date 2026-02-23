using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingDoubleQuotesForConstantString", typeof(Strings), nameof(Strings.AvoidUsingDoubleQuotesForConstantStringDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidUsingDoubleQuotesForConstantString : ScriptRule
    {
        internal AvoidUsingDoubleQuotesForConstantString(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is StringConstantExpressionAst, searchNestedScriptBlocks: true))
            {
                var strAst = (StringConstantExpressionAst)node;

                switch (strAst.StringConstantType)
                {
                    case StringConstantType.DoubleQuoted:
                        if (strAst.Value.Contains("'") || strAst.Extent.Text.Contains("`"))
                        {
                            break;
                        }

                        yield return CreateDiagnosticWithCorrection(strAst, $"'{strAst.Value}'");
                        break;

                    case StringConstantType.DoubleQuotedHereString:
                        if (strAst.Value.Contains("@'") || strAst.Extent.Text.Contains("`"))
                        {
                            break;
                        }

                        yield return CreateDiagnosticWithCorrection(
                            strAst,
                            $"@'{Environment.NewLine}{strAst.Value}{Environment.NewLine}'@");
                        break;

                    default:
                        break;
                }
            }
        }

        private ScriptDiagnostic CreateDiagnosticWithCorrection(StringConstantExpressionAst strAst, string correctionText)
        {
            var corrections = new List<Correction>
            {
                new Correction(strAst.Extent, correctionText, "Use single quotes for constant string")
            };

            return CreateDiagnostic(Strings.AvoidUsingDoubleQuotesForConstantStringError, strAst.Extent, corrections);
        }
    }
}
