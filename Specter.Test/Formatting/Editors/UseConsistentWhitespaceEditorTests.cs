using Specter.Builtin.Editors;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Formatting.Editors
{
    public class UseConsistentWhitespaceEditorTests
    {
        private static string Format(
            string input,
            bool checkOpenBrace = true,
            bool checkInnerBrace = true,
            bool checkPipe = true,
            bool checkPipeForRedundantWhitespace = false,
            bool checkOpenParen = true,
            bool checkOperator = true,
            bool checkSeparator = true,
            bool ignoreAssignmentOperatorInsideHashTable = false)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new UseConsistentWhitespaceEditor(new UseConsistentWhitespaceEditorConfiguration
                {
                    CheckOpenBrace = checkOpenBrace,
                    CheckInnerBrace = checkInnerBrace,
                    CheckPipe = checkPipe,
                    CheckPipeForRedundantWhitespace = checkPipeForRedundantWhitespace,
                    CheckOpenParen = checkOpenParen,
                    CheckOperator = checkOperator,
                    CheckSeparator = checkSeparator,
                    IgnoreAssignmentOperatorInsideHashTable = ignoreAssignmentOperatorInsideHashTable,
                }))
                .Build();

            return formatter.Format(input);
        }

        #region CheckOpenBrace

        [Fact]
        public void OpenBrace_NoSpace_SpaceAdded()
        {
            Assert.Equal("if ($true) {}", Format("if ($true){}"));
        }

        [Fact]
        public void OpenBrace_AlreadyOneSpace_Unchanged()
        {
            string input = "if ($true) {}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void OpenBrace_ExtraSpaces_NormalizedToOne()
        {
            Assert.Equal("if ($true) {}", Format("if ($true)  {}"));
        }

        #endregion

        #region CheckInnerBrace

        [Fact]
        public void InnerBrace_NoSpaceAfterOpen_SpaceAdded()
        {
            Assert.Equal("{ $x }", Format("{$x}"));
        }

        [Fact]
        public void InnerBrace_NoSpaceBeforeClose_SpaceAdded()
        {
            Assert.Equal("{ $x }", Format("{ $x}"));
        }

        [Fact]
        public void InnerBrace_MultiLine_NoChange()
        {
            string input = "{\n    $x\n}";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region CheckPipe

        [Fact]
        public void Pipe_NoSpaceBefore_SpaceAdded()
        {
            Assert.Equal("$x | $y", Format("$x| $y"));
        }

        [Fact]
        public void Pipe_NoSpaceAfter_SpaceAdded()
        {
            Assert.Equal("$x | $y", Format("$x |$y"));
        }

        [Fact]
        public void Pipe_NoSpaceEitherSide_SpacesAdded()
        {
            Assert.Equal("$x | $y", Format("$x|$y"));
        }

        [Fact]
        public void Pipe_RedundantWhitespace_NotFixedByDefault()
        {
            string input = "$x  |  $y";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Pipe_RedundantWhitespace_FixedWhenEnabled()
        {
            Assert.Equal("$x | $y", Format("$x  |  $y", checkPipeForRedundantWhitespace: true));
        }

        #endregion

        #region CheckOpenParen

        [Fact]
        public void KeywordParen_NoSpace_SpaceAdded()
        {
            Assert.Equal("if ($true) {}", Format("if($true) {}"));
        }

        [Fact]
        public void KeywordParen_ExtraSpaces_NormalizedToOne()
        {
            Assert.Equal("if ($true) {}", Format("if  ($true) {}"));
        }

        [Fact]
        public void FunctionCallParen_NotModified()
        {
            // Function calls don't get spacing enforced
            string input = "Test-Function($x)";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region CheckOperator

        [Fact]
        public void Operator_Assignment_NoSpaces_SpacesAdded()
        {
            Assert.Equal("$x = 1", Format("$x=1"));
        }

        [Fact]
        public void Operator_Addition_NoSpaces_SpacesAdded()
        {
            Assert.Equal("$x + $y", Format("$x+$y"));
        }

        [Fact]
        public void Operator_RangeOperator_NotModified()
        {
            string input = "1..10";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Operator_InsideHashtable_IgnoreWhenConfigured()
        {
            string input = "@{\n    Name='Test'\n}";
            // With ignore option enabled, the = inside multi-line hashtable is not modified
            Assert.Equal(input, Format(input, ignoreAssignmentOperatorInsideHashTable: true));
        }

        #endregion

        #region CheckSeparator

        [Fact]
        public void Separator_Comma_NoSpaceAfter_SpaceAdded()
        {
            Assert.Equal("$x, $y", Format("$x,$y"));
        }

        [Fact]
        public void Separator_Comma_AlreadyOneSpace_Unchanged()
        {
            string input = "$x, $y";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Separator_Semicolon_NoSpaceAfter_SpaceAdded()
        {
            Assert.Equal("$x; $y", Format("$x;$y"));
        }

        [Fact]
        public void Separator_AtEndOfLine_NotModified()
        {
            string input = "$x,\n$y";
            Assert.Equal(input, Format(input));
        }

        #endregion

        #region Comment preservation

        [Fact]
        public void Operator_WithComment_CommentPreserved()
        {
            string input = "$x = 1 # assign value";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Pipe_WithInlineComment_CommentPreserved()
        {
            string input = "$x | # pipeline\n    $y";
            string result = Format(input);
            Assert.Contains("# pipeline", result);
        }

        #endregion
    }
}
