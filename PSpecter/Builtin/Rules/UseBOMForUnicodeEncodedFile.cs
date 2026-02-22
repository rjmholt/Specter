#nullable disable

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
    [Rule("UseBOMForUnicodeEncodedFile", typeof(Strings), nameof(Strings.UseBOMForUnicodeEncodedFileDescription))]
    public class UseBOMForUnicodeEncodedFile : ScriptRule
    {
        public UseBOMForUnicodeEncodedFile(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                yield break;
            }

            byte[] byteStream = File.ReadAllBytes(fileName);

            if (DetectBom(byteStream) != null)
            {
                yield break;
            }

            bool hasNonAscii = false;
            foreach (byte b in byteStream)
            {
                if (b > 0x7F)
                {
                    hasNonAscii = true;
                    break;
                }
            }

            if (hasNonAscii)
            {
                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseBOMForUnicodeEncodedFileError,
                        Path.GetFileName(fileName)),
                    ast.Extent);
            }
        }

        private static string DetectBom(byte[] bytes)
        {
            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                return "utf-32BE";
            }

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                return "utf-32";
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return "utf-16BE";
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return "utf-16";
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return "utf-8";
            }

            if (bytes.Length >= 3 && bytes[0] == 0x2B && bytes[1] == 0x2F && bytes[2] == 0x76)
            {
                return "utf-7";
            }

            return null;
        }
    }
}
