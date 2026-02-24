using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseErrorActionPreferenceConsistently", "Flags use of $ErrorActionPreference='Stop' without corresponding try/catch handling.", Severity = DiagnosticSeverity.Information)]
    internal sealed class UseErrorActionPreferenceConsistently : ScriptRule
    {
        internal UseErrorActionPreferenceConsistently(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            bool hasTryCatch = false;
            foreach (Ast found in ast.FindAll(static node => node is TryStatementAst, searchNestedScriptBlocks: true))
            {
                hasTryCatch = true;
                break;
            }

            foreach (Ast found in ast.FindAll(static node => node is AssignmentStatementAst, searchNestedScriptBlocks: true))
            {
                var assignment = (AssignmentStatementAst)found;
                if (assignment.Left is not VariableExpressionAst variableAst)
                {
                    continue;
                }

                if (!string.Equals(variableAst.GetNameWithoutScope(), "ErrorActionPreference", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ExpressionAst? assignedExpression = null;
                if (assignment.Right is PipelineBaseAst pipelineBaseAst)
                {
                    assignedExpression = pipelineBaseAst.GetPureExpression();
                }
                else if (assignment.Right is CommandExpressionAst commandExpressionAst)
                {
                    assignedExpression = commandExpressionAst.Expression;
                }

                if (assignedExpression is null)
                {
                    continue;
                }

                object? assignedValue = AstTools.GetSafeValueFromAst(assignedExpression);
                if (!string.Equals(assignedValue as string, "Stop", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (hasTryCatch)
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Script sets $ErrorActionPreference='Stop' without try/catch. Consider explicit error handling for terminating errors."),
                    assignment.Extent,
                    DiagnosticSeverity.Information);
            }
        }
    }
}
