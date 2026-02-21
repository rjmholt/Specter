// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules
{
    /// <summary>
    /// Checks that lines don't have trailing whitespace.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidTrailingWhitespace", typeof(Strings), nameof(Strings.AvoidTrailingWhitespaceDescription), Severity = DiagnosticSeverity.Information)]
    public class AvoidTrailingWhitespace : ScriptRule
    {
        public AvoidTrailingWhitespace(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null) throw new ArgumentNullException(nameof(ast));

            string[] lines = ast.Extent.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Length == 0)
                {
                    continue;
                }

                char lastChar = line[line.Length - 1];
                if (lastChar != ' ' && lastChar != '\t')
                {
                    continue;
                }

                yield return CreateDiagnostic(Strings.AvoidTrailingWhitespaceError, ast.Extent);
            }
        }
    }
}
