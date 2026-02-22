using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    public class AvoidLongLinesConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enabled: true);

        public int MaximumLineLength { get; set; } = 120;
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidLongLines", typeof(Strings), nameof(Strings.AvoidLongLinesDescription))]
    public class AvoidLongLines : ConfigurableScriptRule<AvoidLongLinesConfiguration>
    {
        private static readonly string[] s_lineSeparators = new[] { "\r\n", "\n" };

        public AvoidLongLines(RuleInfo ruleInfo, AvoidLongLinesConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            int maxLength = Configuration.MaximumLineLength;
            string[] lines = ast.Extent.Text.Split(s_lineSeparators, StringSplitOptions.None);

            for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                string line = lines[lineNumber];

                if (line.Length <= maxLength)
                {
                    continue;
                }

                int startLine = lineNumber + 1;

                var violationExtent = new System.Management.Automation.Language.ScriptExtent(
                    new System.Management.Automation.Language.ScriptPosition(
                        ast.Extent.File, startLine, 1, line),
                    new System.Management.Automation.Language.ScriptPosition(
                        ast.Extent.File, startLine, line.Length, line));

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidLongLinesError, maxLength),
                    violationExtent);
            }
        }
    }
}
