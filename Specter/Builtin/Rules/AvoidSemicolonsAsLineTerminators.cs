using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidSemicolonsAsLineTerminators", typeof(Strings), nameof(Strings.AvoidSemicolonsAsLineTerminatorsDescription))]
    internal class AvoidSemicolonsAsLineTerminators : ScriptRule
    {
        internal AvoidSemicolonsAsLineTerminators(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            IEnumerable<Ast> assignmentStatements = ast.FindAll(static item => item is AssignmentStatementAst, searchNestedScriptBlocks: true);

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (token.Kind != TokenKind.Semi)
                {
                    continue;
                }

                bool isPartOfAssignment = assignmentStatements.Any(
                    stmt => stmt.Extent.EndOffset == token.Extent.StartOffset + 1);

                if (isPartOfAssignment)
                {
                    continue;
                }

                if (i + 1 < tokens.Count)
                {
                    Token nextToken = tokens[i + 1];
                    if (nextToken.Kind != TokenKind.NewLine && nextToken.Kind != TokenKind.EndOfInput)
                    {
                        continue;
                    }
                }

                var corrections = new List<Correction>
                {
                    new Correction(token.Extent, string.Empty, "Remove semicolon")
                };

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidSemicolonsAsLineTerminatorsError),
                    token.Extent,
                    corrections);
            }
        }
    }
}
