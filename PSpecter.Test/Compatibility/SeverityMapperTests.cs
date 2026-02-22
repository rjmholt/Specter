using System;
using PSpecter.PssaCompatibility;
using CompatSeverity = Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticSeverity;
using EngineSeverity = PSpecter.DiagnosticSeverity;
using Xunit;

namespace PSpecter.Test.Compatibility;

public class SeverityMapperTests
{
    [Theory]
    [InlineData(EngineSeverity.Information, CompatSeverity.Information)]
    [InlineData(EngineSeverity.Warning, CompatSeverity.Warning)]
    [InlineData(EngineSeverity.Error, CompatSeverity.Error)]
    [InlineData(EngineSeverity.ParseError, CompatSeverity.ParseError)]
    public void ToCompat_MapsAllSeverities(EngineSeverity engine, CompatSeverity expected)
    {
        Assert.Equal(expected, SeverityMapper.ToCompat(engine));
    }

    [Fact]
    public void ToCompat_UnknownValue_DefaultsToWarning()
    {
        Assert.Equal(CompatSeverity.Warning, SeverityMapper.ToCompat((EngineSeverity)999));
    }

    [Fact]
    public void ToCompat_AllEngineSeverities_AreMapped()
    {
        foreach (EngineSeverity severity in Enum.GetValues<EngineSeverity>())
        {
            var result = SeverityMapper.ToCompat(severity);
            Assert.True(Enum.IsDefined(result), $"Engine severity {severity} maps to undefined compat value");
        }
    }
}
