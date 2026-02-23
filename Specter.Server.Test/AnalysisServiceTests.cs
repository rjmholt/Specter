using Specter.Server;
using Xunit;

namespace Specter.Server.Test;

public class AnalysisServiceTests
{
    private readonly AnalysisService _service;

    public AnalysisServiceTests()
    {
        _service = AnalysisService.CreateDefault();
    }

    [Fact]
    public void CreateDefault_ReturnsNonNullService()
    {
        using var service = AnalysisService.CreateDefault();
        Assert.NotNull(service);
    }

    [Fact]
    public void GetRules_ReturnsNonEmptyList()
    {
        var rules = _service.GetRules();
        Assert.NotEmpty(rules);
    }

    [Fact]
    public void GetRules_ContainsKnownRule()
    {
        var rules = _service.GetRules();
        Assert.Contains(rules, r => r.FullName == "PS/AvoidUsingCmdletAliases");
    }

    [Fact]
    public void AnalyzeScriptContent_ValidScript_ReturnsNoDiagnostics()
    {
        var diagnostics = _service.AnalyzeScriptContent("Get-Process");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AnalyzeScriptContent_ScriptWithAlias_ReturnsDiagnostic()
    {
        var diagnostics = _service.AnalyzeScriptContent("gps");
        Assert.Contains(diagnostics, d => d.Rule?.FullName == "PS/AvoidUsingCmdletAliases");
    }

    [Fact]
    public void AnalyzeScriptContent_EmptyScript_ReturnsNoDiagnostics()
    {
        var diagnostics = _service.AnalyzeScriptContent("");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AnalyzeScriptContent_WithFilePath_ReturnsResults()
    {
        var diagnostics = _service.AnalyzeScriptContent("gps", filePath: "/tmp/test.ps1");
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void OpenDocument_StoresDocument()
    {
        _service.OpenDocument("file:///test.ps1", "Get-Process", 1);
        var doc = _service.GetDocument("file:///test.ps1");
        Assert.NotNull(doc);
        Assert.Equal("Get-Process", doc.Content);
        Assert.Equal(1, doc.Version);
    }

    [Fact]
    public void UpdateDocument_ReplacesContent()
    {
        _service.OpenDocument("file:///test.ps1", "Get-Process", 1);
        _service.UpdateDocument("file:///test.ps1", "Get-Service", 2);

        var doc = _service.GetDocument("file:///test.ps1");
        Assert.NotNull(doc);
        Assert.Equal("Get-Service", doc.Content);
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public void CloseDocument_RemovesDocument()
    {
        _service.OpenDocument("file:///test.ps1", "Get-Process", 1);
        _service.CloseDocument("file:///test.ps1");

        var doc = _service.GetDocument("file:///test.ps1");
        Assert.Null(doc);
    }

    [Fact]
    public void GetDocument_UnknownUri_ReturnsNull()
    {
        var doc = _service.GetDocument("file:///nonexistent.ps1");
        Assert.Null(doc);
    }

    [Fact]
    public void CloseDocument_UnknownUri_DoesNotThrow()
    {
        _service.CloseDocument("file:///nonexistent.ps1");
    }

    [Fact]
    public void DocumentUri_IsCaseInsensitive()
    {
        _service.OpenDocument("file:///Test.PS1", "Get-Process", 1);
        var doc = _service.GetDocument("file:///test.ps1");
        Assert.NotNull(doc);
    }
}
