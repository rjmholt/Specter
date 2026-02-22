using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidMultipleTypeAttributes: Check that parameters don't have more than one type constraint.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidMultipleTypeAttributes", typeof(Strings), nameof(Strings.AvoidMultipleTypeAttributesDescription))]
    public class AvoidMultipleTypeAttributes : ScriptRule
    {
        public AvoidMultipleTypeAttributes(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check that parameters don't have more than one type specifier.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> paramAsts = ast.FindAll(testAst => testAst is ParameterAst, true);

            foreach (ParameterAst paramAst in paramAsts)
            {
                int typeConstraintCount = paramAst.Attributes.Count(attr => attr is TypeConstraintAst);

                if (typeConstraintCount > 1)
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidMultipleTypeAttributesError, paramAst.Name),
                        paramAst.Name.Extent);
                }
            }
        }
    }
}
