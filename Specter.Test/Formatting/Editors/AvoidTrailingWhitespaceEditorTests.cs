using Specter.Rules.Builtin.Editors;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Formatting.Editors
{
    public class AvoidTrailingWhitespaceEditorTests
    {
        private static string Format(string input)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new AvoidTrailingWhitespaceEditor(new AvoidTrailingWhitespaceEditorConfiguration()))
                .Build();

            return formatter.Format(input);
        }

        [Fact]
        public void NoTrailingWhitespace_Unchanged()
        {
            Assert.Equal("Get-Process", Format("Get-Process"));
        }

        [Fact]
        public void TrailingSpace_Removed()
        {
            Assert.Equal("Get-Process", Format("Get-Process "));
        }

        [Fact]
        public void TrailingTab_Removed()
        {
            Assert.Equal("Get-Process", Format("Get-Process\t"));
        }

        [Fact]
        public void TrailingMixedWhitespace_Removed()
        {
            Assert.Equal("Get-Process", Format("Get-Process \t "));
        }

        [Fact]
        public void MultipleLines_TrailingOnEach()
        {
            Assert.Equal("Get-Process\nGet-Service", Format("Get-Process \nGet-Service "));
        }

        [Fact]
        public void MultipleLines_OnlyOneHasTrailing()
        {
            Assert.Equal("Get-Process\nGet-Service", Format("Get-Process\nGet-Service "));
        }

        [Fact]
        public void EmptyLines_Preserved()
        {
            Assert.Equal("Get-Process\n\nGet-Service", Format("Get-Process\n\nGet-Service"));
        }

        [Fact]
        public void CrLf_TrailingBeforeCrLf_Removed()
        {
            Assert.Equal("Get-Process\r\nGet-Service", Format("Get-Process \r\nGet-Service "));
        }

        [Fact]
        public void TrailingOnCommentLine_Removed()
        {
            Assert.Equal("# this is a comment", Format("# this is a comment "));
        }

        [Fact]
        public void TrailingAfterInlineComment_Removed()
        {
            Assert.Equal("Get-Process # inline", Format("Get-Process # inline  "));
        }

        [Fact]
        public void BlockComment_TrailingAfter_Removed()
        {
            Assert.Equal("$x = <# comment #> 5", Format("$x = <# comment #> 5  "));
        }

        [Fact]
        public void LeadingWhitespace_Preserved()
        {
            Assert.Equal("    Get-Process", Format("    Get-Process"));
        }

        [Fact]
        public void LeadingPreserved_TrailingRemoved()
        {
            Assert.Equal("    Get-Process", Format("    Get-Process   "));
        }

        [Fact]
        public void EmptyScript_Unchanged()
        {
            Assert.Equal("", Format(""));
        }

        [Fact]
        public void OnlyWhitespace_Removed()
        {
            Assert.Equal("", Format("   "));
        }

        [Fact]
        public void MultiLineWithTabs_TrailingTabsRemoved()
        {
            Assert.Equal("function Test {\n    $x = 1\n}", Format("function Test {\t\n    $x = 1\t\n}"));
        }
    }
}
