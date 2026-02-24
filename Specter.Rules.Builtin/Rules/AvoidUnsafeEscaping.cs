using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUnsafeEscaping", typeof(Strings), nameof(Strings.AvoidUnsafeEscapingDescription))]
    internal class AvoidUnsafeEscaping : ScriptRule
    {
        internal AvoidUnsafeEscaping(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is BinaryExpressionAst, searchNestedScriptBlocks: true))
            {
                var binaryAst = (BinaryExpressionAst)found;

                if (binaryAst.Operator != TokenKind.Ireplace
                    && binaryAst.Operator != TokenKind.Creplace)
                {
                    continue;
                }

                if (!HasUnsafeEscapingPattern(binaryAst.Right))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    Strings.AvoidUnsafeEscapingError,
                    binaryAst.Extent);
            }
        }

        private static bool HasUnsafeEscapingPattern(ExpressionAst expression)
        {
            string? text = null;

            if (expression is StringConstantExpressionAst constant)
            {
                text = constant.Value;
            }
            else if (expression is ExpandableStringExpressionAst expandable)
            {
                text = expandable.Value;
            }

            if (text is null)
            {
                return false;
            }

            // Detect backtick-based escaping: `' `" `` or doubled-quote escaping ''  ""
            // that indicates manual escaping instead of using CodeGeneration methods
            return text.Contains("`'")
                || text.Contains("`\"")
                || text.Contains("``")
                || text.Contains("''")
                || text.Contains("\"\"");
        }
    }
}
