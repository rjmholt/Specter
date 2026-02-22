using System.Linq;
using PSpecter.PssaCompatibility;
using Xunit;

namespace PSpecter.Test.Compatibility;

public class EditorNameMapperTests
{
    [Theory]
    [InlineData("PSPlaceOpenBrace", "PlaceOpenBrace")]
    [InlineData("PSPlaceCloseBrace", "PlaceCloseBrace")]
    [InlineData("PSUseConsistentWhitespace", "UseConsistentWhitespace")]
    [InlineData("PSUseConsistentIndentation", "UseConsistentIndentation")]
    [InlineData("PSAlignAssignmentStatement", "AlignAssignmentStatement")]
    [InlineData("PSUseCorrectCasing", "UseCorrectCasing")]
    [InlineData("PSAvoidTrailingWhitespace", "AvoidTrailingWhitespace")]
    [InlineData("PSAvoidSemicolonsAsLineTerminators", "AvoidSemicolonsAsLineTerminators")]
    [InlineData("PSAvoidUsingDoubleQuotesForConstantString", "AvoidUsingDoubleQuotesForConstantString")]
    [InlineData("PSAvoidExclaimOperator", "AvoidExclaimOperator")]
    public void TryGetEditorName_KnownRules_ReturnsCorrectMapping(string pssaName, string expectedEditor)
    {
        Assert.True(EditorNameMapper.TryGetEditorName(pssaName, out string? editorName));
        Assert.Equal(expectedEditor, editorName);
    }

    [Fact]
    public void TryGetEditorName_CaseInsensitive()
    {
        Assert.True(EditorNameMapper.TryGetEditorName("psplaceopenbrace", out string? name));
        Assert.Equal("PlaceOpenBrace", name);
    }

    [Fact]
    public void TryGetEditorName_UnknownRule_ReturnsFalse()
    {
        Assert.False(EditorNameMapper.TryGetEditorName("PSNonExistentRule", out _));
    }

    [Fact]
    public void GetAllMappings_ReturnsAllKnownEditorRules()
    {
        var mappings = EditorNameMapper.GetAllMappings().ToList();
        Assert.Equal(10, mappings.Count);
    }

    [Fact]
    public void TryGetPropertyName_UnknownRule_ReturnsOriginalName()
    {
        EditorNameMapper.TryGetPropertyName("PSPlaceOpenBrace", "Enable", out string? propName);
        Assert.Equal("Enable", propName);
    }
}
