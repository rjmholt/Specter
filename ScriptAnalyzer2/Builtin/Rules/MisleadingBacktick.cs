// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    /// <summary>
    /// Checks that lines don't end with a backtick followed by whitespace,
    /// which is misleading because the backtick appears to be a line continuation.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("MisleadingBacktick", typeof(Strings), nameof(Strings.MisleadingBacktickDescription))]
    public class MisleadingBacktick : ScriptRule
    {
        private static readonly Regex s_trailingEscapedWhitespace = new Regex(@"`\s+$", RegexOptions.Compiled);

        public MisleadingBacktick(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null) throw new ArgumentNullException(nameof(ast));

            string[] lines = ast.Extent.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = s_trailingEscapedWhitespace.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }

                int lineNumber = ast.Extent.StartLineNumber + i;
                yield return CreateDiagnostic(Strings.MisleadingBacktickError, ast.Extent);
            }
        }
    }
}
