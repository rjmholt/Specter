// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast foundAst in ast.FindAll(testAst => testAst is BinaryExpressionAst, searchNestedScriptBlocks: true))
            {
                var binExpr = (BinaryExpressionAst)foundAst;

                if (!s_equalityOperators.Contains(binExpr.Operator))
                {
                    continue;
                }

                if (!binExpr.Right.Extent.Text.Equals("$null", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (binExpr.Left is VariableExpressionAst leftVar
                    && leftVar.IsSpecialVariable())
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
    }
}
