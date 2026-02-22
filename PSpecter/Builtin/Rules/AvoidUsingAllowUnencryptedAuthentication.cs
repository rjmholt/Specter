using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// AvoidUsingAllowUnencryptedAuthentication: Avoid sending credentials and secrets over unencrypted connections.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingAllowUnencryptedAuthentication", typeof(Strings), nameof(Strings.AvoidUsingAllowUnencryptedAuthenticationDescription))]
    public class AvoidUsingAllowUnencryptedAuthentication : ScriptRule
    {
        public AvoidUsingAllowUnencryptedAuthentication(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that AllowUnencryptedAuthentication parameter is not used.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> commandAsts = ast.FindAll(testAst => testAst is CommandAst, true);
            if (commandAsts == null)
            {
                yield break;
            }

            foreach (CommandAst cmdAst in commandAsts)
            {
                if (cmdAst.CommandElements == null)
                {
                    continue;
                }

                foreach (CommandElementAst element in cmdAst.CommandElements)
                {
                    if (element is CommandParameterAst paramAst
                        && string.Equals(paramAst.ParameterName, "AllowUnencryptedAuthentication", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateDiagnostic(Strings.AvoidUsingAllowUnencryptedAuthenticationError, paramAst);
                        break;
                    }
                }
            }
        }
    }
}
