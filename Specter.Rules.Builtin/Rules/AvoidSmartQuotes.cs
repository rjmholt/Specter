using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidSmartQuotes", typeof(Strings), nameof(Strings.AvoidSmartQuotesDescription))]
    internal class AvoidSmartQuotes : ScriptRule
    {
        private static readonly Dictionary<char, string> s_typographicChars = new Dictionary<char, string>
        {
            { '\u2018', "'" },  // left single curly quote
            { '\u2019', "'" },  // right single curly quote
            { '\u201C', "\"" }, // left double curly quote
            { '\u201D', "\"" }, // right double curly quote
            { '\u2013', "-" },  // en dash
            { '\u2014', "-" },  // em dash
        };

        internal AvoidSmartQuotes(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string text = ast.Extent.Text;
            string? filePath = ast.Extent.File;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (!s_typographicChars.ContainsKey(c))
                {
                    continue;
                }

                var extent = ScriptExtent.FromOffsets(text, filePath, i, i + 1);

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AvoidSmartQuotesError,
                        c,
                        (int)c),
                    extent);
            }
        }
    }
}
