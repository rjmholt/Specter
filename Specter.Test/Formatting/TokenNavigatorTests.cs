using System.Management.Automation.Language;
using Specter.Formatting;
using Xunit;

namespace Specter.Test.Formatting
{
    public class TokenNavigatorTests
    {
        private static TokenNavigator ParseAndNavigate(string script)
        {
            Parser.ParseInput(script, out Token[] tokens, out _);
            return new TokenNavigator(tokens);
        }

        [Fact]
        public void HasCommentInRange_WithComment_ReturnsTrue()
        {
            // "$x = <# comment #> 5"
            var nav = ParseAndNavigate("$x = <# comment #> 5");

            bool hasComment = nav.HasCommentInRange(0, nav.Tokens[nav.Count - 1].Extent.EndOffset);

            Assert.True(hasComment);
        }

        [Fact]
        public void HasCommentInRange_WithoutComment_ReturnsFalse()
        {
            var nav = ParseAndNavigate("$x = 5");

            bool hasComment = nav.HasCommentInRange(0, nav.Tokens[nav.Count - 1].Extent.EndOffset);

            Assert.False(hasComment);
        }

        [Fact]
        public void HasCommentInRange_CommentOutsideRange_ReturnsFalse()
        {
            // "Get-Process # comment"
            var nav = ParseAndNavigate("Get-Process # comment");

            // Check only the range of "Get-Process" (offsets 0..11)
            bool hasComment = nav.HasCommentInRange(0, 11);

            Assert.False(hasComment);
        }

        [Fact]
        public void NextTokenIgnoringComments_SkipsComments()
        {
            // "Get-Process <# c #> -Name foo"
            var nav = ParseAndNavigate("Get-Process <# c #> -Name foo");

            // Find index of "Get-Process" token
            int getProcessIdx = 0;
            int nextIdx = nav.NextTokenIgnoringComments(getProcessIdx);

            Assert.True(nextIdx > 0);
            Assert.NotEqual(TokenKind.Comment, nav[nextIdx].Kind);
        }

        [Fact]
        public void NextSignificantToken_SkipsNewlinesAndComments()
        {
            var nav = ParseAndNavigate("Get-Process\n# comment\nGet-Service");

            int nextIdx = nav.NextSignificantToken(0);

            Assert.True(nextIdx > 0);
            Assert.Equal("Get-Service", nav[nextIdx].Text);
        }

        [Fact]
        public void FindTokenAtOffset_ReturnsCorrectToken()
        {
            var nav = ParseAndNavigate("$x = 5");

            int idx = nav.FindTokenAtOffset(0);

            Assert.True(idx >= 0);
            Assert.Equal("$x", nav[idx].Text);
        }

        [Fact]
        public void PreviousSignificantToken_SkipsNewlinesAndComments()
        {
            var nav = ParseAndNavigate("Get-Process\n# comment\nGet-Service");

            // Find Get-Service token index
            int serviceIdx = -1;
            for (int i = 0; i < nav.Count; i++)
            {
                if (nav[i].Text == "Get-Service")
                {
                    serviceIdx = i;
                    break;
                }
            }

            Assert.True(serviceIdx >= 0);
            int prevIdx = nav.PreviousSignificantToken(serviceIdx);
            Assert.True(prevIdx >= 0);
            Assert.Equal("Get-Process", nav[prevIdx].Text);
        }
    }
}
