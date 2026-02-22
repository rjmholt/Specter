using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Formatting;
using Xunit;

namespace PSpecter.Test.Formatting
{
    public class ScriptFormatterTests
    {
        [Fact]
        public void EmptyEditorList_ReturnsInputUnchanged()
        {
            var formatter = new ScriptFormatter.Builder().Build();

            string result = formatter.Format("Get-Process ");

            Assert.Equal("Get-Process ", result);
        }

        [Fact]
        public void SingleEditor_AppliesEdits()
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new TrailingSpaceRemover())
                .Build();

            string result = formatter.Format("Get-Process \nGet-Service ");

            Assert.Equal("Get-Process\nGet-Service", result);
        }

        [Fact]
        public void MultipleEditors_AppliedInOrder()
        {
            // First editor removes trailing spaces, second replaces tabs with spaces
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new TrailingSpaceRemover())
                .AddEditor(new TabToSpaceReplacer())
                .Build();

            string result = formatter.Format("\tGet-Process ");

            Assert.Equal("    Get-Process", result);
        }

        [Fact]
        public void Editor_ProducingNoEdits_DoesNotCorruptContent()
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new NoOpEditor())
                .AddEditor(new TrailingSpaceRemover())
                .Build();

            string result = formatter.Format("Get-Process ");

            Assert.Equal("Get-Process", result);
        }

        #region Test editors

        private sealed class NoOpEditor : IScriptEditor
        {
            public IReadOnlyList<ScriptEdit> GetEdits(string scriptContent, Ast ast, IReadOnlyList<Token> tokens, string filePath)
            {
                return new List<ScriptEdit>();
            }
        }

        private sealed class TrailingSpaceRemover : IScriptEditor
        {
            public IReadOnlyList<ScriptEdit> GetEdits(string scriptContent, Ast ast, IReadOnlyList<Token> tokens, string filePath)
            {
                var edits = new List<ScriptEdit>();
                string[] lines = scriptContent.Split('\n');
                int offset = 0;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.TrimEnd('\r');
                    int trimmedLength = line.TrimEnd(' ', '\t').Length;

                    if (trimmedLength < line.Length)
                    {
                        edits.Add(new ScriptEdit(offset + trimmedLength, offset + line.Length, string.Empty));
                    }

                    offset += rawLine.Length + 1; // +1 for the \n
                }

                return edits;
            }
        }

        private sealed class TabToSpaceReplacer : IScriptEditor
        {
            public IReadOnlyList<ScriptEdit> GetEdits(string scriptContent, Ast ast, IReadOnlyList<Token> tokens, string filePath)
            {
                var edits = new List<ScriptEdit>();

                for (int i = 0; i < scriptContent.Length; i++)
                {
                    if (scriptContent[i] == '\t')
                    {
                        edits.Add(new ScriptEdit(i, i + 1, "    "));
                    }
                }

                return edits;
            }
        }

        #endregion
    }
}
