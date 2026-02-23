using Specter.PssaCompatibility;
using Xunit;

namespace Specter.Test.Compatibility;

public class RuleNameMapperTests
{
    [Theory]
    [InlineData("PSAvoidUsingCmdletAliases", "PS/AvoidUsingCmdletAliases")]
    [InlineData("PSAvoidUsingWriteHost", "PS/AvoidUsingWriteHost")]
    [InlineData("PSUseApprovedVerbs", "PS/UseApprovedVerbs")]
    public void ToEngineFullName_PsPrefix_SplitsCorrectly(string pssa, string expected)
    {
        Assert.Equal(expected, RuleNameMapper.ToEngineFullName(pssa));
    }

    [Theory]
    [InlineData("PSDSCReturnCorrectTypesForDSCFunctions", "PSDSC/ReturnCorrectTypesForDSCFunctions")]
    [InlineData("PSDSCUseVerboseMessageInDSCResource", "PSDSC/UseVerboseMessageInDSCResource")]
    [InlineData("PSDSCDscTestsPresent", "PSDSC/DscTestsPresent")]
    public void ToEngineFullName_PsdscPrefix_SplitsCorrectly(string pssa, string expected)
    {
        Assert.Equal(expected, RuleNameMapper.ToEngineFullName(pssa));
    }

    [Theory]
    [InlineData("PS/AvoidUsingWriteHost")]
    [InlineData("PSDSC/DscTestsPresent")]
    [InlineData("Custom/MyRule")]
    public void ToEngineFullName_AlreadyEngineFormat_ReturnsUnchanged(string name)
    {
        Assert.Equal(name, RuleNameMapper.ToEngineFullName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToEngineFullName_NullOrEmpty_ReturnsAsIs(string? input)
    {
        Assert.Equal(input, RuleNameMapper.ToEngineFullName(input!));
    }

    [Theory]
    [InlineData("lowercase", "lowercase")]
    [InlineData("NoPrefixRule", "NoPrefixRule")]
    public void ToEngineFullName_NoKnownPrefix_ReturnsUnchanged(string input, string expected)
    {
        Assert.Equal(expected, RuleNameMapper.ToEngineFullName(input));
    }

    [Theory]
    [InlineData("PS/AvoidUsingCmdletAliases", "PSAvoidUsingCmdletAliases")]
    [InlineData("PS/UseApprovedVerbs", "PSUseApprovedVerbs")]
    [InlineData("PSDSC/DscTestsPresent", "PSDSCDscTestsPresent")]
    public void ToPssaRuleName_EngineFormat_ConcatenatesCorrectly(string engine, string expected)
    {
        Assert.Equal(expected, RuleNameMapper.ToPssaRuleName(engine));
    }

    [Theory]
    [InlineData("PSAvoidUsingWriteHost")]
    [InlineData("SomeRuleWithoutSlash")]
    public void ToPssaRuleName_NoSlash_ReturnsUnchanged(string name)
    {
        Assert.Equal(name, RuleNameMapper.ToPssaRuleName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToPssaRuleName_NullOrEmpty_ReturnsAsIs(string? input)
    {
        Assert.Equal(input, RuleNameMapper.ToPssaRuleName(input!));
    }

    [Theory]
    [InlineData("AvoidUsingWriteHost", "PSAvoidUsingWriteHost")]
    [InlineData("UseApprovedVerbs", "PSUseApprovedVerbs")]
    public void ToPssaName_ShortName_AddsPsPrefix(string shortName, string expected)
    {
        Assert.Equal(expected, RuleNameMapper.ToPssaName(shortName));
    }

    [Theory]
    [InlineData("PSAvoidUsingWriteHost")]
    [InlineData("PSDSCDscTestsPresent")]
    public void ToPssaName_AlreadyPrefixed_ReturnsUnchanged(string name)
    {
        Assert.Equal(name, RuleNameMapper.ToPssaName(name));
    }

    [Fact]
    public void IsMatch_ExactMatch_ReturnsTrue()
    {
        Assert.True(RuleNameMapper.IsMatch("PSAvoidUsingWriteHost", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(RuleNameMapper.IsMatch("psavoidusingwritehost", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_DifferentNames_ReturnsFalse()
    {
        Assert.False(RuleNameMapper.IsMatch("PSAvoidUsingWriteHost", "PSUseApprovedVerbs"));
    }

    [Fact]
    public void IsMatch_WildcardSuffix_MatchesPrefix()
    {
        Assert.True(RuleNameMapper.IsMatch("PSAvoid*", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_WildcardPrefix_MatchesSuffix()
    {
        Assert.True(RuleNameMapper.IsMatch("*WriteHost", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_WildcardMiddle_Matches()
    {
        Assert.True(RuleNameMapper.IsMatch("PS*Host", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_WildcardAll_MatchesEverything()
    {
        Assert.True(RuleNameMapper.IsMatch("*", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void IsMatch_WildcardNoMatch_ReturnsFalse()
    {
        Assert.False(RuleNameMapper.IsMatch("PSUse*", "PSAvoidUsingWriteHost"));
    }

    [Fact]
    public void ToEngineFullName_PsLowercasePrefix_NotRecognized()
    {
        // "ps" followed by lowercase letter is NOT a recognized PS prefix
        Assert.Equal("psnotaprefix", RuleNameMapper.ToEngineFullName("psnotaprefix"));
    }

    [Fact]
    public void Roundtrip_PsRule_PreservesName()
    {
        string pssa = "PSAvoidUsingWriteHost";
        string engine = RuleNameMapper.ToEngineFullName(pssa);
        string roundtripped = RuleNameMapper.ToPssaRuleName(engine);
        Assert.Equal(pssa, roundtripped);
    }

    [Fact]
    public void Roundtrip_PsdscRule_PreservesName()
    {
        string pssa = "PSDSCDscTestsPresent";
        string engine = RuleNameMapper.ToEngineFullName(pssa);
        string roundtripped = RuleNameMapper.ToPssaRuleName(engine);
        Assert.Equal(pssa, roundtripped);
    }
}
