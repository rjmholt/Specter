#nullable disable

using System;
using System.Collections.Generic;
using PSpecter.Formatting;
using Xunit;

namespace PSpecter.Test.Formatting
{
    public class ScriptFormatBufferTests
    {
        [Fact]
        public void FromScript_ParsesContent()
        {
            var buffer = ScriptFormatBuffer.FromScript("Get-Process", null);

            Assert.Equal("Get-Process", buffer.Content);
            Assert.NotNull(buffer.Ast);
            Assert.NotNull(buffer.Tokens);
            Assert.True(buffer.Tokens.Count > 0);
        }

        [Fact]
        public void ApplyEdits_SingleEdit_ReplacesContent()
        {
            var buffer = ScriptFormatBuffer.FromScript("Get-Process ", null);

            bool applied = buffer.ApplyEdits(new[]
            {
                new ScriptEdit(11, 12, string.Empty)
            });

            Assert.True(applied);
            Assert.Equal("Get-Process", buffer.Content);
        }

        [Fact]
        public void ApplyEdits_SingleEdit_UpdatesAst()
        {
            var buffer = ScriptFormatBuffer.FromScript("Get-Process ", null);

            buffer.ApplyEdits(new[] { new ScriptEdit(11, 12, string.Empty) });

            Assert.Equal("Get-Process", buffer.Ast.Extent.Text);
        }

        [Fact]
        public void ApplyEdits_MultipleNonOverlapping_AppliesAllCorrectly()
        {
            // "aa  bb  cc" -> "aa bb cc"
            var buffer = ScriptFormatBuffer.FromScript("$aa  + $bb  + $cc", null);

            bool applied = buffer.ApplyEdits(new[]
            {
                new ScriptEdit(3, 5, " "),
                new ScriptEdit(10, 12, " "),
            });

            Assert.True(applied);
            Assert.Equal("$aa + $bb + $cc", buffer.Content);
        }

        [Fact]
        public void ApplyEdits_EmptyList_ReturnsFalse()
        {
            var buffer = ScriptFormatBuffer.FromScript("Get-Process", null);

            bool applied = buffer.ApplyEdits(new List<ScriptEdit>());

            Assert.False(applied);
            Assert.Equal("Get-Process", buffer.Content);
        }

        [Fact]
        public void ApplyEdits_Null_ReturnsFalse()
        {
            var buffer = ScriptFormatBuffer.FromScript("Get-Process", null);

            bool applied = buffer.ApplyEdits(null);

            Assert.False(applied);
        }

        [Fact]
        public void ApplyEdits_OverlappingEdits_Throws()
        {
            var buffer = ScriptFormatBuffer.FromScript("$abcdefghij", null);

            Assert.Throws<InvalidOperationException>(() =>
            {
                buffer.ApplyEdits(new[]
                {
                    new ScriptEdit(1, 5, "X"),
                    new ScriptEdit(3, 8, "Y"),
                });
            });
        }

        [Fact]
        public void ApplyEdits_InsertionAtSamePoint_DoesNotOverlap()
        {
            var buffer = ScriptFormatBuffer.FromScript("$ab", null);

            // Two adjacent edits (not overlapping since one ends where the other starts)
            bool applied = buffer.ApplyEdits(new[]
            {
                new ScriptEdit(1, 2, "X"),
                new ScriptEdit(2, 3, "Y"),
            });

            Assert.True(applied);
            Assert.Equal("$XY", buffer.Content);
        }

        [Fact]
        public void RoundTrip_NoEdits_ContentUnchanged()
        {
            const string script = "function Test {\n    Get-Process\n}\n";
            var buffer = ScriptFormatBuffer.FromScript(script, null);

            Assert.Equal(script, buffer.Content);
            Assert.Equal(script, buffer.ToString());
        }
    }
}
