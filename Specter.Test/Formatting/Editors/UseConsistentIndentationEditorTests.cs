using Specter.Builtin.Editors;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Formatting.Editors
{
    public class UseConsistentIndentationEditorTests
    {
        private static string Format(
            string input,
            int indentationSize = 4,
            bool useTabs = false,
            PipelineIndentationStyle pipelineIndentation = PipelineIndentationStyle.IncreaseIndentationForFirstPipeline)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new UseConsistentIndentationEditor(new UseConsistentIndentationEditorConfiguration
                {
                    IndentationSize = indentationSize,
                    UseTabs = useTabs,
                    PipelineIndentation = pipelineIndentation,
                }))
                .Build();

            return formatter.Format(input);
        }

        #region Basic indentation

        [Fact]
        public void SingleLevel_CorrectIndent()
        {
            string input = "if ($true) {\n$x = 1\n}";
            string expected = "if ($true) {\n    $x = 1\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void NestedLevels_CorrectIndent()
        {
            string input = "if ($true) {\nif ($false) {\n$x = 1\n}\n}";
            string expected = "if ($true) {\n    if ($false) {\n        $x = 1\n    }\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void AlreadyCorrectlyIndented_Unchanged()
        {
            string input = "if ($true) {\n    $x = 1\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void OverIndented_Reduced()
        {
            string input = "if ($true) {\n        $x = 1\n}";
            string expected = "if ($true) {\n    $x = 1\n}";
            Assert.Equal(expected, Format(input));
        }

        #endregion

        #region Indentation size

        [Fact]
        public void IndentationSize2_Works()
        {
            string input = "if ($true) {\n$x = 1\n}";
            string expected = "if ($true) {\n  $x = 1\n}";
            Assert.Equal(expected, Format(input, indentationSize: 2));
        }

        [Fact]
        public void TabIndentation_Works()
        {
            string input = "if ($true) {\n$x = 1\n}";
            string expected = "if ($true) {\n\t$x = 1\n}";
            Assert.Equal(expected, Format(input, useTabs: true));
        }

        #endregion

        #region Functions

        [Fact]
        public void Function_BodyIndented()
        {
            string input = "function Test {\n$x = 1\nreturn $x\n}";
            string expected = "function Test {\n    $x = 1\n    return $x\n}";
            Assert.Equal(expected, Format(input));
        }

        #endregion

        #region Parentheses

        [Fact]
        public void MultiLineParen_Indented()
        {
            string input = "$x = (\n1 + 2\n)";
            string expected = "$x = (\n    1 + 2\n)";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void SingleLineParen_NotAffected()
        {
            string input = "$x = (1 + 2)";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region Hashtables

        [Fact]
        public void Hashtable_BodyIndented()
        {
            string input = "$h = @{\nName = 'Test'\nValue = 42\n}";
            string expected = "$h = @{\n    Name = 'Test'\n    Value = 42\n}";
            Assert.Equal(expected, Format(input));
        }

        #endregion

        #region Pipeline indentation

        [Fact]
        public void Pipeline_FirstPipelineStyle_IndentsAfterFirstPipe()
        {
            string input = "Get-Process |\nWhere-Object { $_.Name } |\nSelect-Object Name";
            string expected = "Get-Process |\n    Where-Object { $_.Name } |\n    Select-Object Name";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void Pipeline_NoneStyle_NoIndent()
        {
            string input = "Get-Process |\nWhere-Object { $_.Name }";
            Assert.Equal(input, Format(input, pipelineIndentation: PipelineIndentationStyle.None));
        }

        #endregion

        #region Comment preservation

        [Fact]
        public void Comment_InsideBlock_IndentedWithCode()
        {
            string input = "if ($true) {\n# comment\n$x = 1\n}";
            string expected = "if ($true) {\n    # comment\n    $x = 1\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void InlineComment_Preserved()
        {
            string input = "if ($true) {\n    $x = 1 # comment\n}";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region Close brace alignment

        [Fact]
        public void CloseBrace_AlignedWithOpeningStatement()
        {
            string input = "if ($true) {\n    $x = 1\n    }";
            string expected = "if ($true) {\n    $x = 1\n}";
            Assert.Equal(expected, Format(input));
        }

        #endregion
    }
}
