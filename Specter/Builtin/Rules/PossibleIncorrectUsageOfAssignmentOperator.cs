using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("PossibleIncorrectUsageOfAssignmentOperator", typeof(Strings), nameof(Strings.PossibleIncorrectUsageOfAssignmentOperatorDescription), Severity = DiagnosticSeverity.Information)]
    internal class PossibleIncorrectUsageOfAssignmentOperator : ScriptRule
    {
        internal PossibleIncorrectUsageOfAssignmentOperator(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is WhileStatementAst || testAst is DoWhileStatementAst, searchNestedScriptBlocks: true))
            {
                var loopAst = (LoopStatementAst)node;
                ScriptDiagnostic? diagnostic = AnalyzeCondition(loopAst.Condition, scriptPath);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is IfStatementAst, searchNestedScriptBlocks: true))
            {
                var ifAst = (IfStatementAst)node;
                foreach (var clause in ifAst.Clauses)
                {
                    ScriptDiagnostic? diagnostic = AnalyzeCondition(clause.Item1, scriptPath);
                    if (diagnostic != null)
                    {
                        yield return diagnostic;
                    }
                }
            }
        }

        private ScriptDiagnostic? AnalyzeCondition(PipelineBaseAst condition, string? scriptPath)
        {
            var assignment = condition.Find(
                testAst => testAst is AssignmentStatementAst,
                searchNestedScriptBlocks: false) as AssignmentStatementAst;

            if (assignment == null)
            {
                return null;
            }

            if (assignment.Right.Extent.Text.StartsWith("="))
            {
                return CreateDiagnostic(
                    Strings.PossibleIncorrectUsageOfAssignmentOperatorError,
                    assignment.ErrorPosition,
                    DiagnosticSeverity.Warning);
            }

            if (assignment.Left is VariableExpressionAst variable
                && variable.VariablePath.UserPath.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDiagnostic(
                    Strings.PossibleIncorrectUsageOfAssignmentOperatorError,
                    assignment.ErrorPosition,
                    DiagnosticSeverity.Warning);
            }

            Ast? commandAst = assignment.Right.Find(testAst => testAst is CommandAst, searchNestedScriptBlocks: true);
            Ast? invokeMemberAst = assignment.Right.Find(testAst => testAst is InvokeMemberExpressionAst, searchNestedScriptBlocks: true);
            bool clangSuppression = condition.Extent.IsWrappedInParentheses();

            if (commandAst == null && invokeMemberAst == null && !clangSuppression)
            {
                return CreateDiagnostic(
                    Strings.PossibleIncorrectUsageOfAssignmentOperatorError,
                    assignment.ErrorPosition,
                    DiagnosticSeverity.Information);
            }

            return null;
        }
    }
}
