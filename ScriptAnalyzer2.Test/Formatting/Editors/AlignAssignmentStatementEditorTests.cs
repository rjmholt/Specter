using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Editors;
using Microsoft.PowerShell.ScriptAnalyzer.Formatting;
using Xunit;

namespace ScriptAnalyzer2.Test.Formatting.Editors
{
    public class AlignAssignmentStatementEditorTests
    {
        private static string Format(string input, bool checkHashtable = true)
        {
            var formatter = new ScriptFormatter.Builder()
                .AddEditor(new AlignAssignmentStatementEditor(new AlignAssignmentStatementEditorConfiguration
                {
                    CheckHashtable = checkHashtable,
                }))
                .Build();

            return formatter.Format(input);
        }

        [Fact]
        public void Hashtable_AlreadyAligned_Unchanged()
        {
            string input = "@{\n    Name  = 'Test'\n    Value = 42\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Hashtable_Unaligned_Aligned()
        {
            string input = "@{\n    Name = 'Test'\n    Value = 42\n}";
            string expected = "@{\n    Name  = 'Test'\n    Value = 42\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void Hashtable_ThreeKeys_AllAligned()
        {
            string input = "@{\n    A = 1\n    BB = 2\n    CCC = 3\n}";
            string expected = "@{\n    A   = 1\n    BB  = 2\n    CCC = 3\n}";
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void SingleLineHashtable_NotModified()
        {
            string input = "@{ Name = 'Test'; Value = 42 }";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void SingleKeyHashtable_NotModified()
        {
            string input = "@{\n    Name = 'Test'\n}";
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void CheckHashtable_False_NotModified()
        {
            string input = "@{\n    Name = 'Test'\n    Value = 42\n}";
            Assert.Equal(input, Format(input, checkHashtable: false));
        }

        [Fact]
        public void Hashtable_WithComments_CommentsPreserved()
        {
            string input = "@{\n    Name = 'Test' # the name\n    Value = 42\n}";
            string result = Format(input);
            Assert.Contains("# the name", result);
        }
    }
}
