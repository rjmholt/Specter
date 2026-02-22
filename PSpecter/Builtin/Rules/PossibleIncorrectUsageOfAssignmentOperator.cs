#nullable disable

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("PossibleIncorrectUsageOfAssignmentOperator", typeof(Strings), nameof(Strings.PossibleIncorrectUsageOfAssignmentOperatorDescription), Severity = DiagnosticSeverity.Information)]
    public class PossibleIncorrectUsageOfAssignmentOperator : ScriptRule
    {
        public PossibleIncorrectUsageOfAssignmentOperator(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is WhileStatementAst || testAst is DoWhileStatementAst, searchNestedScriptBlocks: true))
            {
                var loopAst = (LoopStatementAst)node;
                ScriptDiagnostic diagnostic = AnalyzeCondition(loopAst.Condition, fileName);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is IfStatementAst, searchNestedScriptBlocks: true))
            {
                var ifAst = (IfStatementAst)node;
                foreach (var clause in ifAst.Clauses)
                {
                    ScriptDiagnostic diagnostic = AnalyzeCondition(clause.Item1, fileName);
                    if (diagnostic != null)
                    {
                        yield return diagnostic;
                    }
                }
            }
        }

        private ScriptDiagnostic AnalyzeCondition(PipelineBaseAst condition, string fileName)
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

            var commandAst = assignment.Right.Find(testAst => testAst is CommandAst, searchNestedScriptBlocks: true);
            var invokeMemberAst = assignment.Right.Find(testAst => testAst is InvokeMemberExpressionAst, searchNestedScriptBlocks: true);
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
