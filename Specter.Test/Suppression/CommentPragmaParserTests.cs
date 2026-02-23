using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Suppression;
using Xunit;

namespace Specter.Test.Suppression
{
    public class CommentPragmaParserTests
    {
        private static Dictionary<string, List<RuleSuppression>> Parse(string script)
        {
            Parser.ParseInput(script, out Token[] tokens, out ParseError[] _);
            var suppressions = new Dictionary<string, List<RuleSuppression>>(System.StringComparer.OrdinalIgnoreCase);
            CommentPragmaParser.CollectFromTokens(tokens, suppressions);
            return suppressions;
        }

        [Fact]
        public void LineSuppression_SuppressesNextLine()
        {
            string script = @"
# specter-suppress PS/AvoidUsingWriteHost
Write-Host 'hello'
Write-Host 'world'
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
            List<RuleSuppression> list = suppressions["PS/AvoidUsingWriteHost"];
            Assert.Single(list);
            Assert.Equal(3, list[0].StartLineNumber);
            Assert.Equal(3, list[0].EndLineNumber);
        }

        [Fact]
        public void InlineSuppression_SuppressesCurrentLine()
        {
            string script = @"Write-Host 'hello' # specter-suppress PS/AvoidUsingWriteHost
Write-Host 'world'
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
            List<RuleSuppression> list = suppressions["PS/AvoidUsingWriteHost"];
            Assert.Single(list);
            Assert.Equal(1, list[0].StartLineNumber);
            Assert.Equal(1, list[0].EndLineNumber);
        }

        [Fact]
        public void BlockSuppression_SuppressesRange()
        {
            string script = @"
# specter-suppress-begin PS/AvoidUsingWriteHost
Write-Host 'a'
Write-Host 'b'
# specter-suppress-end PS/AvoidUsingWriteHost
Write-Host 'c'
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
            List<RuleSuppression> list = suppressions["PS/AvoidUsingWriteHost"];
            Assert.Single(list);
            Assert.True(list[0].StartLineNumber <= 2);
            Assert.True(list[0].EndLineNumber >= 5);
        }

        [Fact]
        public void MultipleRulesInSinglePragma()
        {
            string script = @"
# specter-suppress PS/AvoidUsingWriteHost, PS/AvoidGlobalVars
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
            Assert.True(suppressions.ContainsKey("PS/AvoidGlobalVars"));
        }

        [Fact]
        public void CaseInsensitivePragma()
        {
            string script = @"
# Specter-Suppress PS/AvoidUsingWriteHost
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
        }

        [Fact]
        public void IgnoresNonPragmaComments()
        {
            string script = @"
# This is just a comment
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.Empty(suppressions);
        }

        [Fact]
        public void UnmatchedBlockEnd_IsIgnored()
        {
            string script = @"
# specter-suppress-end PS/AvoidUsingWriteHost
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.Empty(suppressions);
        }

        [Fact]
        public void UnmatchedBlockBegin_DoesNotCreate()
        {
            string script = @"
# specter-suppress-begin PS/AvoidUsingWriteHost
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.Empty(suppressions);
        }

        [Fact]
        public void NestedBlocks_WorkCorrectly()
        {
            string script = @"
# specter-suppress-begin PS/AvoidUsingWriteHost
Write-Host 'a'
# specter-suppress-begin PS/AvoidUsingWriteHost
Write-Host 'b'
# specter-suppress-end PS/AvoidUsingWriteHost
Write-Host 'c'
# specter-suppress-end PS/AvoidUsingWriteHost
";
            var suppressions = Parse(script);
            Assert.True(suppressions.ContainsKey("PS/AvoidUsingWriteHost"));
            Assert.Equal(2, suppressions["PS/AvoidUsingWriteHost"].Count);
        }

        [Fact]
        public void PragmaWithoutRuleName_IsIgnored()
        {
            string script = @"
# specter-suppress
Write-Host 'hello'
";
            var suppressions = Parse(script);
            Assert.Empty(suppressions);
        }

    }
}
