using Specter.Builtin.Editors;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Formatting.Editors
{
    public class UseCorrectCasingEditorTests
    {
        private static string Format(string input, bool checkKeyword = true, bool checkOperator = true)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new UseCorrectCasingEditor(new UseCorrectCasingEditorConfiguration
                {
                    CheckKeyword = checkKeyword,
                    CheckOperator = checkOperator,
                }))
                .Build();

            return formatter.Format(input);
        }

        #region Keywords

        [Fact]
        public void Keyword_AlreadyLowercase_Unchanged()
        {
            string input = "if ($true) { }";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Keyword_Uppercase_Lowered()
        {
            Assert.Equal("if ($true) { }", Format("IF ($true) { }"));
        }

        [Fact]
        public void Keyword_MixedCase_Lowered()
        {
            Assert.Equal("foreach ($x in $y) { }", Format("ForEach ($x in $y) { }"));
        }

        [Fact]
        public void Keyword_Function_Lowered()
        {
            Assert.Equal("function Test { }", Format("Function Test { }"));
        }

        [Fact]
        public void Keyword_Multiple_AllLowered()
        {
            Assert.Equal("if ($true) { } else { }", Format("IF ($true) { } ELSE { }"));
        }

        [Fact]
        public void Keyword_Disabled_NotModified()
        {
            string input = "IF ($true) { }";
            Assert.Equal(input, Format(input, checkKeyword: false));
        }

        #endregion

        #region Operators

        [Fact]
        public void Operator_AlreadyLowercase_Unchanged()
        {
            string input = "$x -eq $y";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Operator_Uppercase_Lowered()
        {
            Assert.Equal("$x -eq $y", Format("$x -EQ $y"));
        }

        [Fact]
        public void Operator_Disabled_NotModified()
        {
            string input = "$x -EQ $y";
            Assert.Equal(input, Format(input, checkOperator: false));
        }

        #endregion

        #region Comment preservation

        [Fact]
        public void Keyword_WithComment_CommentPreserved()
        {
            string input = "IF ($true) { } # check";
            string result = Format(input);
            Assert.Contains("# check", result);
            Assert.StartsWith("if", result);
        }

        #endregion
    }
}
