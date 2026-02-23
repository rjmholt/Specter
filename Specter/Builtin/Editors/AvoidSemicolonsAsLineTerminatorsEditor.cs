using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Formatting;

namespace Specter.Builtin.Editors
{
    internal sealed class AvoidSemicolonsAsLineTerminatorsEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enabled);
    }

    [Editor("AvoidSemicolonsAsLineTerminators", Description = "Removes semicolons used as line terminators")]
    internal sealed class AvoidSemicolonsAsLineTerminatorsEditor : IScriptEditor, IConfigurableEditor<AvoidSemicolonsAsLineTerminatorsEditorConfiguration>
    {
        internal AvoidSemicolonsAsLineTerminatorsEditor(AvoidSemicolonsAsLineTerminatorsEditorConfiguration configuration)
        {
            Configuration = configuration ?? new AvoidSemicolonsAsLineTerminatorsEditorConfiguration();
        }

        public AvoidSemicolonsAsLineTerminatorsEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();
            IEnumerable<Ast> assignmentStatements = ast.FindAll(
                static item => item is AssignmentStatementAst,
                searchNestedScriptBlocks: true);

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (token.Kind != TokenKind.Semi)
                {
                    continue;
                }

                bool isPartOfAssignment = assignmentStatements.Any(
                    stmt => stmt.Extent.EndOffset == token.Extent.StartOffset + 1);

                if (isPartOfAssignment)
                {
                    continue;
                }

                if (i + 1 < tokens.Count)
                {
                    Token nextToken = tokens[i + 1];
                    if (nextToken.Kind != TokenKind.NewLine && nextToken.Kind != TokenKind.EndOfInput)
                    {
                        continue;
                    }
                }

                edits.Add(new ScriptEdit(
                    token.Extent.StartOffset,
                    token.Extent.EndOffset,
                    string.Empty));
            }

            return edits;
        }
    }
}
