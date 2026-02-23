using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// Checks that $null is on the left side of any equality comparisons.
    /// When an array is on the left side, PowerShell filters for $null in the array
    /// rather than testing whether the array itself is null.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PossibleIncorrectComparisonWithNull", typeof(Strings), nameof(Strings.PossibleIncorrectComparisonWithNullDescription))]
    public class PossibleIncorrectComparisonWithNull : ScriptRule
    {
        private static readonly HashSet<TokenKind> s_equalityOperators = new HashSet<TokenKind>
        {
            TokenKind.Ieq,
            TokenKind.Ine,
            TokenKind.Ceq,
            TokenKind.Cne,
            TokenKind.Equals,
        };

        public PossibleIncorrectComparisonWithNull(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast foundAst in ast.FindAll(static testAst => testAst is BinaryExpressionAst, searchNestedScriptBlocks: true))
            {
                var binExpr = (BinaryExpressionAst)foundAst;

                if (!IsIncorrectNullComparison(binExpr))
                {
                    continue;
                }

                string swapped = $"{binExpr.Right.Extent.Text} {binExpr.ErrorPosition.Text} {binExpr.Left.Extent.Text}";
                var correction = new Correction(binExpr.Extent, swapped, Strings.PossibleIncorrectComparisonWithNullSuggesteCorrectionDescription);

                yield return CreateDiagnostic(
                    Strings.PossibleIncorrectComparisonWithNullError,
                    binExpr,
                    new[] { correction });
            }
        }

        /// <summary>
        /// Determines whether the comparison could be problematic: $null on the right
        /// side and the left side could plausibly be an array, which changes
        /// PowerShell's comparison semantics from equality-test to filter.
        /// </summary>
        private static bool IsIncorrectNullComparison(BinaryExpressionAst binExpr)
        {
            if (!s_equalityOperators.Contains(binExpr.Operator))
            {
                return false;
            }

            if (!binExpr.Right.Extent.Text.Equals("$null", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ExpressionAst left = binExpr.Left;

            if (left.StaticType?.IsArray == true)
            {
                return true;
            }

            if (left is VariableExpressionAst leftVar)
            {
                if (leftVar.IsSpecialVariable())
                {
                    return false;
                }

                // Without full type inference, a variable with StaticType == object
                // could be anything including an array. Try a simple assignment lookup;
                // if we can resolve to a concrete non-array/non-object type, skip it.
                Type? resolvedType = TryResolveVariableType(leftVar);
                if (resolvedType is null)
                {
                    return true;
                }

                return resolvedType.IsArray
                    || resolvedType == typeof(object)
                    || resolvedType == typeof(void);
            }

            // For non-variable expressions (constants, method calls, etc.),
            // only flag if the static type is object (truly unknown).
            return left.StaticType == typeof(object);
        }

        /// <summary>
        /// Walks up the AST to find the enclosing scope (function body or script block)
        /// and looks for a simple assignment to the variable. If found, returns the
        /// StaticType of the assigned expression.
        /// </summary>
        private static Type? TryResolveVariableType(VariableExpressionAst variable)
        {
            string? varName = variable.VariablePath.UserPath;

            Ast? scope = FindEnclosingScope(variable);
            if (scope is null)
            {
                return null;
            }

            foreach (Ast node in scope.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)node;

                if (assignment.Left is not VariableExpressionAst target)
                {
                    continue;
                }

                if (varName is null || !string.Equals(target.VariablePath.UserPath, varName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Only trust simple assignments (=), not compound ones (+=, etc.)
                if (assignment.Operator != TokenKind.Equals)
                {
                    continue;
                }

                ExpressionAst? rhs = (assignment.Right as PipelineBaseAst)?.GetPureExpression()
                    ?? (assignment.Right as CommandExpressionAst)?.Expression;

                if (rhs is not null && rhs.StaticType is not null && rhs.StaticType != typeof(object))
                {
                    return rhs.StaticType;
                }
            }

            return null;
        }

        private static Ast? FindEnclosingScope(Ast node)
        {
            for (Ast current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is FunctionDefinitionAst
                    || (current is ScriptBlockAst scriptBlock && scriptBlock.Parent is null))
                {
                    return current;
                }
            }

            return null;
        }
    }
}
