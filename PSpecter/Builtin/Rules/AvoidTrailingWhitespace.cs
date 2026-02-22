#nullable disable

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidTrailingWhitespace", typeof(Strings), nameof(Strings.AvoidTrailingWhitespaceDescription), Severity = DiagnosticSeverity.Information)]
    public class AvoidTrailingWhitespace : ScriptRule, IFormattingRule
    {
        public AvoidTrailingWhitespace(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public IScriptEditor CreateEditor() => new AvoidTrailingWhitespaceEditor(new AvoidTrailingWhitespaceEditorConfiguration());

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

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

                int trailingStart = line.Length - 1;
                while (trailingStart > 0 && (line[trailingStart - 1] == ' ' || line[trailingStart - 1] == '\t'))
                {
                    trailingStart--;
                }

                int lineNumber = ast.Extent.StartLineNumber + i;
                int startColumn = trailingStart + 1;
                int endColumn = line.Length + 1;
                string whitespaceText = line.Substring(trailingStart);

                var extent = new ScriptExtent(
                    whitespaceText,
                    new ScriptPosition(null, fileName, line, 0, lineNumber, startColumn),
                    new ScriptPosition(null, fileName, line, 0, lineNumber, endColumn));

                var correction = new Correction(extent, string.Empty, Strings.AvoidTrailingWhitespaceError);

                yield return CreateDiagnostic(Strings.AvoidTrailingWhitespaceError, extent, new[] { correction });
            }
        }
    }
}
