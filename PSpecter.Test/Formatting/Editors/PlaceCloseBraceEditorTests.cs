using System;
using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using Xunit;

namespace PSpecter.Test.Formatting.Editors
{
    public class PlaceCloseBraceEditorTests
    {
        private static string Format(string input, bool noEmptyLineBefore = false, bool ignoreOneLineBlock = true, bool newLineAfter = true)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new PlaceCloseBraceEditor(new PlaceCloseBraceEditorConfiguration
                {
                    NoEmptyLineBefore = noEmptyLineBefore,
                    IgnoreOneLineBlock = ignoreOneLineBlock,
                    NewLineAfter = newLineAfter,
                }))
                .Build();

            return formatter.Format(input);
        }

        #region Close brace on own line

        [Fact]
        public void CloseBrace_AlreadyOnOwnLine_Unchanged()
        {
            string input = "if ($true) {\n    $x\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void CloseBrace_NotOnOwnLine_MovedToOwnLine()
        {
            string input = "if ($true) {\n    $x }";
            string result = Format(input);
            Assert.Contains(Environment.NewLine + "}", result);
        }

        #endregion

        #region NoEmptyLineBefore

        [Fact]
        public void NoEmptyLineBefore_False_EmptyLineKept()
        {
            string input = "if ($true) {\n    $x\n\n}";
            Assert.Equal(input, Format(input, noEmptyLineBefore: false));
        }

        [Fact]
        public void NoEmptyLineBefore_True_EmptyLineRemoved()
        {
            string input = "if ($true) {\n    $x\n\n}";
            string result = Format(input, noEmptyLineBefore: true);
            Assert.DoesNotContain("\n\n}", result);
        }

        #endregion

        #region NewLineAfter (branch keywords)

        [Fact]
        public void NewLineAfter_True_CuddledElse_NewLineInserted()
        {
            string input = "if ($true) {\n    $x\n} else {\n    $y\n}";
            string result = Format(input, newLineAfter: true);
            Assert.Contains("}" + Environment.NewLine + " else", result);
        }

        [Fact]
        public void NewLineAfter_True_ElseAlreadyOnNewLine_Unchanged()
        {
            string input = "if ($true) {\n    $x\n}\nelse {\n    $y\n}";
            Assert.Equal(input, Format(input, newLineAfter: true));
        }

        [Fact]
        public void NewLineAfter_False_ElseOnNewLine_Cuddled()
        {
            string input = "if ($true) {\n    $x\n}\nelse {\n    $y\n}";
            string result = Format(input, newLineAfter: false);
            Assert.Contains("} else", result);
        }

        [Fact]
        public void NewLineAfter_False_CatchOnNewLine_Cuddled()
        {
            string input = "try {\n    $x\n}\ncatch {\n    $y\n}";
            string result = Format(input, newLineAfter: false);
            Assert.Contains("} catch", result);
        }

        [Fact]
        public void NewLineAfter_False_FinallyOnNewLine_Cuddled()
        {
            string input = "try {\n    $x\n}\nfinally {\n    $y\n}";
            string result = Format(input, newLineAfter: false);
            Assert.Contains("} finally", result);
        }

        [Fact]
        public void NewLineAfter_False_ElseIfOnNewLine_Cuddled()
        {
            string input = "if ($true) {\n    $x\n}\nelseif ($false) {\n    $y\n}";
            string result = Format(input, newLineAfter: false);
            Assert.Contains("} elseif", result);
        }

        #endregion

        #region IgnoreOneLineBlock

        [Fact]
        public void IgnoreOneLineBlock_True_OneLineBlock_Unchanged()
        {
            string input = "$x = if ($true) { 'a' } else { 'b' }";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region Hashtables

        [Fact]
        public void OneLineHashtable_Unchanged()
        {
            string input = "$h = @{ Name = 'Test' }";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region Comment preservation

        [Fact]
        public void CloseBrace_WithCommentOnPrecedingLine_CommentPreserved()
        {
            string input = "if ($true) {\n    $x # comment\n}";
            string result = Format(input);
            Assert.Contains("# comment", result);
            Assert.Equal(input, result);
        }

        [Fact]
        public void CloseBrace_WithCommentAfter_CommentPreserved()
        {
            string input = "if ($true) {\n    $x\n} # end if";
            string result = Format(input);
            Assert.Contains("# end if", result);
        }

        #endregion
    }
}
