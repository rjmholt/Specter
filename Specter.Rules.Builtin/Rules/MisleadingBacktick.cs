using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("MisleadingBacktick", typeof(Strings), nameof(Strings.MisleadingBacktickDescription))]
    internal class MisleadingBacktick : ScriptRule
    {
        private static readonly Regex s_trailingEscapedWhitespace = new Regex(@"`(\s+)$", RegexOptions.Compiled);

        internal MisleadingBacktick(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string[] lines = ast.Extent.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = s_trailingEscapedWhitespace.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }

                int lineNumber = ast.Extent.StartLineNumber + i;
                string line = lines[i];

                // The correction covers only the whitespace after the backtick
                Group whitespaceGroup = match.Groups[1];
                int startColumn = whitespaceGroup.Index + 1;
                int endColumn = line.Length + 1;
                string whitespaceText = whitespaceGroup.Value;

                var extent = new ScriptExtent(
                    whitespaceText,
                    new ScriptPosition(string.Empty, scriptPath, line, 0, lineNumber, startColumn),
                    new ScriptPosition(string.Empty, scriptPath, line, 0, lineNumber, endColumn));

                var correction = new Correction(extent, string.Empty, Strings.MisleadingBacktickError);

                yield return CreateDiagnostic(Strings.MisleadingBacktickError, extent, new[] { correction });
            }
        }
    }
}
