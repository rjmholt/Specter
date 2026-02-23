using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Builtin.Editors
{
    internal sealed class AvoidTrailingWhitespaceEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enable);
    }

    [Editor("AvoidTrailingWhitespace", Description = "Removes trailing spaces and tabs from all lines")]
    internal sealed class AvoidTrailingWhitespaceEditor : IScriptEditor, IConfigurableEditor<AvoidTrailingWhitespaceEditorConfiguration>
    {
        internal AvoidTrailingWhitespaceEditor(AvoidTrailingWhitespaceEditorConfiguration configuration)
        {
            Configuration = configuration ?? new AvoidTrailingWhitespaceEditorConfiguration();
        }

        public AvoidTrailingWhitespaceEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            int lineStart = 0;

            while (lineStart <= scriptContent.Length)
            {
                int lineEnd = scriptContent.IndexOf('\n', lineStart);
                bool isLastLine = lineEnd < 0;
                if (isLastLine)
                {
                    lineEnd = scriptContent.Length;
                }

                int contentEnd = lineEnd;
                if (contentEnd > lineStart && scriptContent[contentEnd - 1] == '\r')
                {
                    contentEnd--;
                }

                int trailingStart = contentEnd;
                while (trailingStart > lineStart
                    && (scriptContent[trailingStart - 1] == ' ' || scriptContent[trailingStart - 1] == '\t'))
                {
                    trailingStart--;
                }

                if (trailingStart < contentEnd)
                {
                    edits.Add(new ScriptEdit(trailingStart, contentEnd, string.Empty));
                }

                if (isLastLine)
                {
                    break;
                }

                lineStart = lineEnd + 1;
            }

            return edits;
        }
    }
}
