using PSpecter.Server;
using PSpecter.Server.Grpc;
using Xunit;

namespace PSpecter.Server.Test;

public class GrpcAnalysisServiceTests
{
    private readonly AnalysisService _analysisService;
    private readonly GrpcAnalysisService _grpcService;

    public GrpcAnalysisServiceTests()
    {
        _analysisService = AnalysisService.CreateDefault();
        _grpcService = new GrpcAnalysisService(_analysisService);
    }

    [Fact]
    public async Task AnalyzeScript_ValidScript_ReturnsEmptyDiagnostics()
    {
        var request = new AnalyzeScriptRequest { ScriptContent = "Get-Process" };
        var response = await _grpcService.AnalyzeScript(request, null!);
        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeScript_ScriptWithViolation_ReturnsDiagnostics()
    {
        var request = new AnalyzeScriptRequest { ScriptContent = "gps" };
        var response = await _grpcService.AnalyzeScript(request, null!);

        Assert.NotEmpty(response.Diagnostics);
        var diag = response.Diagnostics[0];
        Assert.Equal("AvoidUsingCmdletAliases", diag.RuleName);
        Assert.Equal("PS/AvoidUsingCmdletAliases", diag.RuleId);
        Assert.Equal(Grpc.DiagnosticSeverity.Warning, diag.Severity);
        Assert.False(string.IsNullOrEmpty(diag.Message));
    }

    [Fact]
    public async Task AnalyzeScript_DiagnosticHasExtent()
    {
        var request = new AnalyzeScriptRequest { ScriptContent = "gps" };
        var response = await _grpcService.AnalyzeScript(request, null!);

        Assert.NotEmpty(response.Diagnostics);
        var extent = response.Diagnostics[0].Extent;
        Assert.NotNull(extent);
        Assert.Equal(1, extent.StartLine);
        Assert.True(extent.EndColumn > extent.StartColumn);
    }

    [Fact]
    public async Task AnalyzeScript_WithFilePath_StillReturnsResults()
    {
        var request = new AnalyzeScriptRequest
        {
            ScriptContent = "gps",
            FilePath = "/tmp/test.ps1"
        };
        var response = await _grpcService.AnalyzeScript(request, null!);
        Assert.NotEmpty(response.Diagnostics);
    }

    [Fact]
    public async Task GetRules_ReturnsNonEmptyRuleList()
    {
        var response = await _grpcService.GetRules(new GetRulesRequest(), null!);
        Assert.NotEmpty(response.Rules);
    }

    [Fact]
    public async Task GetRules_RulesHaveRequiredFields()
    {
        var response = await _grpcService.GetRules(new GetRulesRequest(), null!);
        foreach (var rule in response.Rules)
        {
            Assert.False(string.IsNullOrEmpty(rule.Name), $"Rule has empty Name");
            Assert.False(string.IsNullOrEmpty(rule.Id), $"Rule {rule.Name} has empty Id");
            Assert.NotEqual(Grpc.DiagnosticSeverity.Unspecified, rule.DefaultSeverity);
        }
    }

    [Fact]
    public async Task GetRules_ContainsDscRules()
    {
        var response = await _grpcService.GetRules(new GetRulesRequest(), null!);
        Assert.Contains(response.Rules, r => r.Id == "PSDSC/ReturnCorrectTypesForDSCFunctions");
    }

    [Fact]
    public async Task FormatScript_ReturnsUnchanged()
    {
        var request = new FormatScriptRequest { ScriptContent = "Get-Process" };
        var response = await _grpcService.FormatScript(request, null!);

        Assert.Equal("Get-Process", response.FormattedContent);
        Assert.False(response.Changed);
    }

    [Fact]
    public async Task AnalyzeScript_SeverityMapping_Information()
    {
        // DSC rules default to Information severity
        var request = new AnalyzeScriptRequest
        {
            ScriptContent = @"
function Set-TargetResource { param($Name) }
function Get-TargetResource { param($Name) return @{} }
function Test-TargetResource { param($Name) return @{} }
"
        };
        var response = await _grpcService.AnalyzeScript(request, null!);
        var dscDiag = response.Diagnostics
            .Where(d => d.RuleId.StartsWith("PSDSC/"))
            .ToList();

        if (dscDiag.Count > 0)
        {
            Assert.All(dscDiag, d => Assert.Equal(Grpc.DiagnosticSeverity.Information, d.Severity));
        }
    }
}
