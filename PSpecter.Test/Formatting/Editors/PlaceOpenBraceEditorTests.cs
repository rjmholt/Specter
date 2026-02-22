using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using Xunit;

namespace PSpecter.Test.Formatting.Editors
{
    public class PlaceOpenBraceEditorTests
    {
        private static string Format(string input, bool onSameLine = true, bool newLineAfter = true, bool ignoreOneLineBlock = true)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new PlaceOpenBraceEditor(new PlaceOpenBraceEditorConfiguration
                {
                    OnSameLine = onSameLine,
                    NewLineAfter = newLineAfter,
                    IgnoreOneLineBlock = ignoreOneLineBlock,
                }))
                .Build();

            return formatter.Format(input);
        }

        #region OnSameLine=true (K&R / OTBS)

        [Fact]
        public void OnSameLine_BraceAlreadyOnSameLine_Unchanged()
        {
            string input = "if ($true) {\n    $x\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void OnSameLine_BraceOnNewLine_MovedToSameLine()
        {
            string input = "if ($true)\n{\n    $x\n}";
            string expected = "if ($true) {\n    $x\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void OnSameLine_BraceOnNewLine_WithComment_CommentPreserved()
        {
            string input = "if ($true) # comment\n{\n    $x\n}";
            string expected = "if ($true) { # comment\n    $x\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void OnSameLine_FunctionBrace_MovedToSameLine()
        {
            string input = "function Test\n{\n    $x\n}";
            string expected = "function Test {\n    $x\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void OnSameLine_ForEachBrace_MovedToSameLine()
        {
            string input = "foreach ($x in $y)\n{\n    $x\n}";
            string expected = "foreach ($x in $y) {\n    $x\n}";
            Assert.Equal(expected, Format(input));
        }

        #endregion

        #region OnSameLine=false (Allman)

        [Fact]
        public void NotOnSameLine_BraceAlreadyOnNewLine_Unchanged()
        {
            string input = "if ($true)\n{\n    $x\n}";
            Assert.Equal(input, Format(input, onSameLine: false));
        }

        [Fact]
        public void NotOnSameLine_BraceOnSameLine_MovedToNewLine()
        {
            string input = "if ($true) {\n    $x\n}";
            string expected = "if ($true)\n{\n    $x\n}";
            Assert.Equal(expected, Format(input, onSameLine: false));
        }

        [Fact]
        public void NotOnSameLine_IndentedBrace_IndentationPreserved()
        {
            string input = "    if ($true) {\n        $x\n    }";
            string expected = "    if ($true)\n    {\n        $x\n    }";
            Assert.Equal(expected, Format(input, onSameLine: false));
        }

        #endregion

        #region NewLineAfter

        [Fact]
        public void NewLineAfter_AlreadyHasNewLine_Unchanged()
        {
            string input = "if ($true) {\n    $x\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void NewLineAfter_NoNewLine_NewLineInserted()
        {
            string input = "if ($true) { $x }";
            // IgnoreOneLineBlock=false to exercise this path
            string result = Format(input, ignoreOneLineBlock: false);
            Assert.Contains("{\n", result);
        }

        #endregion

        #region IgnoreOneLineBlock

        [Fact]
        public void IgnoreOneLineBlock_True_OneLineIfElse_Unchanged()
        {
            string input = "$x = if ($true) { 'a' } else { 'b' }";
            Assert.Equal(input, Format(input, onSameLine: false, ignoreOneLineBlock: true));
        }

        [Fact]
        public void IgnoreOneLineBlock_False_OneLineBlock_Modified()
        {
            string input = "if ($true) { $x }";
            string result = Format(input, ignoreOneLineBlock: false);
            // With newLineAfter=true and ignoreOneLineBlock=false,
            // a newline should be inserted after the brace
            Assert.Contains("{\n", result);
        }

        #endregion

        #region Command-element script blocks

        [Fact]
        public void CommandElementScriptBlock_NotModified()
        {
            // Pipeline script blocks should never be reformatted
            string input = "Get-Process | Where-Object\n{ $_.Name }";
            string result = Format(input);
            // The brace is a command element, so even with OnSameLine=true it should not move
            Assert.Equal(input, result);
        }

        #endregion
    }
}
