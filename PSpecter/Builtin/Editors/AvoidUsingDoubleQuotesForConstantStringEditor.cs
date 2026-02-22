using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Formatting;

namespace PSpecter.Builtin.Editors
{
    public sealed class AvoidUsingDoubleQuotesForConstantStringEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration { Enabled = false };
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enabled);
    }

    [Editor("AvoidUsingDoubleQuotesForConstantString", Description = "Replaces double-quoted constant strings with single-quoted equivalents")]
    public sealed class AvoidUsingDoubleQuotesForConstantStringEditor : IScriptEditor, IConfigurableEditor<AvoidUsingDoubleQuotesForConstantStringEditorConfiguration>
    {
        public AvoidUsingDoubleQuotesForConstantStringEditor(AvoidUsingDoubleQuotesForConstantStringEditorConfiguration configuration)
        {
            Configuration = configuration ?? new AvoidUsingDoubleQuotesForConstantStringEditorConfiguration();
        }

        public AvoidUsingDoubleQuotesForConstantStringEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();

            foreach (Ast node in ast.FindAll(testAst => testAst is StringConstantExpressionAst, searchNestedScriptBlocks: true))
            {
                var strAst = (StringConstantExpressionAst)node;

                switch (strAst.StringConstantType)
                {
                    case StringConstantType.DoubleQuoted:
                        if (strAst.Value.Contains("'") || strAst.Extent.Text.Contains("`"))
                        {
                            break;
                        }

                        edits.Add(new ScriptEdit(
                            strAst.Extent.StartOffset,
                            strAst.Extent.EndOffset,
                            $"'{strAst.Value}'"));
                        break;

                    case StringConstantType.DoubleQuotedHereString:
                        if (strAst.Value.Contains("@'") || strAst.Extent.Text.Contains("`"))
                        {
                            break;
                        }

                        edits.Add(new ScriptEdit(
                            strAst.Extent.StartOffset,
                            strAst.Extent.EndOffset,
                            $"@'{Environment.NewLine}{strAst.Value}{Environment.NewLine}'@"));
                        break;

                    default:
                        break;
                }
            }

            return edits;
        }
    }
}
