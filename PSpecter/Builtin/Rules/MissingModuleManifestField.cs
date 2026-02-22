using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("MissingModuleManifestField", typeof(Strings), nameof(Strings.MissingModuleManifestFieldDescription))]
    public class MissingModuleManifestField : ScriptRule
    {
        private static readonly RequiredField[] s_requiredFields = new[]
        {
            new RequiredField(
                name: "ModuleVersion",
                comment: "# Version number of this module.",
                defaultValue: "'1.0.0.0'"),
        };

        public MissingModuleManifestField(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (scriptPath is null || !AstExtensions.IsModuleManifest(scriptPath))
            {
                yield break;
            }

            HashtableAst hashtable = ast
                .FindAll(node => node is HashtableAst, searchNestedScriptBlocks: false)
                .OfType<HashtableAst>()
                .FirstOrDefault()!;

            if (hashtable is null)
            {
                yield break;
            }

            var presentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyAst)
                {
                    presentKeys.Add(keyAst.Value);
                }
            }

            if (!LooksLikeModuleManifest(presentKeys))
            {
                yield break;
            }

            foreach (RequiredField required in s_requiredFields)
            {
                if (presentKeys.Contains(required.Name))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    "The member '{0}' is not present in the module manifest.",
                    required.Name);

                var correction = BuildInsertionCorrection(hashtable, required);

                yield return CreateDiagnostic(
                    message,
                    hashtable,
                    correction is not null ? new[] { correction } : (IReadOnlyList<Correction>?)null);
            }
        }

        private static Correction BuildInsertionCorrection(HashtableAst hashtable, RequiredField required)
        {
            string nl = Environment.NewLine;
            string correctionText = nl + required.Comment + nl + required.Name + " = " + required.DefaultValue + nl;
            string description = string.Format(
                CultureInfo.CurrentCulture,
                Strings.MissingModuleManifestFieldCorrectionDescription,
                required.Name,
                required.DefaultValue);

            int insertLine = hashtable.Extent.StartLineNumber;
            int insertColumn = hashtable.Extent.StartColumnNumber + hashtable.Extent.Text.IndexOf('{') + 1;

            var insertPosition = new ScriptPosition(
                hashtable.Extent.File!,
                startLineNumber: insertLine,
                startColumnNumber: insertColumn);

            var insertExtent = new ScriptExtent(insertPosition, insertPosition);

            return new Correction(insertExtent, correctionText, description);
        }

        private static bool LooksLikeModuleManifest(HashSet<string> keys)
        {
            return keys.Contains("GUID")
                || keys.Contains("Author")
                || keys.Contains("RootModule")
                || keys.Contains("ModuleToProcess")
                || keys.Contains("ModuleVersion");
        }

        private readonly struct RequiredField
        {
            public readonly string Name;
            public readonly string Comment;
            public readonly string DefaultValue;

            public RequiredField(string name, string comment, string defaultValue)
            {
                Name = name;
                Comment = comment;
                DefaultValue = defaultValue;
            }
        }

        internal readonly struct ScriptPosition : IScriptPosition
        {
            private readonly string _file;
            private readonly int _line;
            private readonly int _column;

            public ScriptPosition(string file, int startLineNumber, int startColumnNumber)
            {
                _file = file;
                _line = startLineNumber;
                _column = startColumnNumber;
            }

            public string File => _file;
            public int LineNumber => _line;
            public int ColumnNumber => _column;
            public int Offset => 0;
            public string Line => string.Empty;

            public string GetFullScript() => string.Empty;
        }

        internal readonly struct ScriptExtent : IScriptExtent
        {
            private readonly IScriptPosition _start;
            private readonly IScriptPosition _end;

            public ScriptExtent(IScriptPosition start, IScriptPosition end)
            {
                _start = start;
                _end = end;
            }

            public string File => _start.File ?? string.Empty;
            public IScriptPosition StartScriptPosition => _start;
            public IScriptPosition EndScriptPosition => _end;
            public int StartLineNumber => _start.LineNumber;
            public int StartColumnNumber => _start.ColumnNumber;
            public int EndLineNumber => _end.LineNumber;
            public int EndColumnNumber => _end.ColumnNumber;
            public int StartOffset => _start.Offset;
            public int EndOffset => _end.Offset;
            public string Text => string.Empty;
        }
    }
}
