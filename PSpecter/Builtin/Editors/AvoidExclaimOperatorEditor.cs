using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Formatting;

namespace PSpecter.Builtin.Editors
{
    internal sealed class AvoidExclaimOperatorEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration { Enabled = false };
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(enabled: Common.Enabled);
    }

    [Editor("AvoidExclaimOperator", Description = "Replaces ! with -not")]
    internal sealed class AvoidExclaimOperatorEditor : IScriptEditor, IConfigurableEditor<AvoidExclaimOperatorEditorConfiguration>
    {
        internal AvoidExclaimOperatorEditor(AvoidExclaimOperatorEditorConfiguration configuration)
        {
            Configuration = configuration ?? new AvoidExclaimOperatorEditorConfiguration();
        }

        public AvoidExclaimOperatorEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();

            foreach (Ast foundAst in ast.FindAll(static node => node is UnaryExpressionAst, searchNestedScriptBlocks: true))
            {
                var unary = (UnaryExpressionAst)foundAst;
                if (unary.TokenKind != TokenKind.Exclaim)
                {
                    continue;
                }

                int exclaimStart = unary.Extent.StartOffset;
                int childStart = unary.Child.Extent.StartOffset;

                // Replace "!" and any space before the child with "-not "
                edits.Add(new ScriptEdit(exclaimStart, childStart, "-not "));
            }

            return edits;
        }
    }
}
