// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidExclaimOperator: Checks for use of the exclaim operator.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidExclaimOperator", typeof(Strings), nameof(Strings.AvoidExclaimOperatorDescription))]
    public class AvoidExclaimOperator : ConfigurableScriptRule<AvoidExclaimOperatorConfiguration>
    {
        public AvoidExclaimOperator(RuleInfo ruleInfo, AvoidExclaimOperatorConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        /// <summary>
        /// Analyzes the given ast to find violations.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            if (!Configuration.Common.Enabled)
            {
                yield break;
            }

            IEnumerable<Ast> foundAsts = ast.FindAll(testAst => testAst is UnaryExpressionAst, true);
            if (foundAsts != null)
            {
                foreach (UnaryExpressionAst unaryExpressionAst in foundAsts)
                {
                    if (unaryExpressionAst.TokenKind == TokenKind.Exclaim)
                    {
                        yield return CreateDiagnostic(Strings.AvoidExclaimOperatorError, unaryExpressionAst);
                    }
                }
            }
        }
    }

    public record AvoidExclaimOperatorConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enabled: false);
    }
}
