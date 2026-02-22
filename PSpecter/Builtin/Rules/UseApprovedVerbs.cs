// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// UseApprovedVerbs: Analyzes scripts to check that all defined functions use approved verbs.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseApprovedVerbs", typeof(Strings), nameof(Strings.UseApprovedVerbsDescription))]
    public class UseApprovedVerbs : ScriptRule
    {
        private static readonly IReadOnlyList<string> s_approvedVerbs = GetApprovedVerbs();

        public UseApprovedVerbs(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that all defined functions use approved verbs.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> funcAsts = ast.FindAll(testAst => testAst is FunctionDefinitionAst, true);
            if (funcAsts == null)
            {
                yield break;
            }

            foreach (FunctionDefinitionAst funcAst in funcAsts)
            {
                string funcName = FunctionNameWithoutScope(funcAst.Name);
                if (funcName == null || !funcName.Contains('-'))
                {
                    continue;
                }

                string[] funcNamePieces = funcName.Split('-');
                string verb = funcNamePieces[0];

                if (!s_approvedVerbs.Contains(verb, StringComparer.OrdinalIgnoreCase))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.UseApprovedVerbsError, funcName),
                        funcAst);
                }
            }
        }

        private static string FunctionNameWithoutScope(string name)
        {
            if (name == null)
            {
                return null;
            }

            string[] scopePrefixes = { "Global:", "Local:", "Script:", "Private:" };
            foreach (string prefix in scopePrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(prefix.Length);
                }
            }

            return name;
        }

        private static IReadOnlyList<string> GetApprovedVerbs()
        {
            var verbTypes = new[]
            {
                typeof(VerbsCommon),
                typeof(VerbsCommunications),
                typeof(VerbsData),
                typeof(VerbsDiagnostic),
                typeof(VerbsLifecycle),
                typeof(VerbsSecurity),
                typeof(VerbsOther),
            };

            var verbs = new List<string>();
            foreach (Type verbType in verbTypes)
            {
                FieldInfo[] fields = verbType.GetFields();
                foreach (FieldInfo field in fields)
                {
                    if (field.IsLiteral)
                    {
                        verbs.Add(field.Name);
                    }
                }
            }

            return verbs;
        }
    }
}
