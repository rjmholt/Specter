using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseUTF8EncodingForHelpFile", typeof(Strings), nameof(Strings.UseUTF8EncodingForHelpFileDescription))]
    internal class UseUTF8EncodingForHelpFile : ScriptRule
    {
        internal UseUTF8EncodingForHelpFile(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
            {
                yield break;
            }

            if (!IsHelpFile(scriptPath))
            {
                yield break;
            }

            using (var fileStream = File.Open(scriptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true))
            {
                reader.ReadToEnd();
                if (reader.CurrentEncoding != System.Text.Encoding.UTF8)
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseUTF8EncodingForHelpFileError,
                            Path.GetFileName(scriptPath),
                            reader.CurrentEncoding),
                        ast.Extent);
                }
            }
        }

        private static bool IsHelpFile(string filePath)
        {
            string name = Path.GetFileName(filePath);
            return name.StartsWith("about_", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".help.txt", StringComparison.OrdinalIgnoreCase);
        }
    }
}
