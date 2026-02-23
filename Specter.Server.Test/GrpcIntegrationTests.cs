using System.Net.Http.Json;
using Specter.Server.Grpc;
using Xunit;

namespace Specter.Server.Test;

public class GrpcIntegrationTests : IClassFixture<GrpcServerFixture>
{
    private readonly GrpcServerFixture _fixture;
    private readonly SpecterAnalysis.SpecterAnalysisClient _client;

    public GrpcIntegrationTests(GrpcServerFixture fixture)
    {
        _fixture = fixture;
        _client = new SpecterAnalysis.SpecterAnalysisClient(fixture.Channel);
    }

    [Fact]
    public async Task AnalyzeScript_CleanScript_ReturnsEmptyDiagnostics()
    {
        var response = await _client.AnalyzeScriptAsync(new AnalyzeScriptRequest
        {
            ScriptContent = "Get-Process",
        });

        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeScript_ScriptWithAlias_ReturnsDiagnostics()
    {
        var response = await _client.AnalyzeScriptAsync(new AnalyzeScriptRequest
        {
            ScriptContent = "gps",
        });

        Assert.NotEmpty(response.Diagnostics);
        Assert.Contains(response.Diagnostics, d => d.RuleName == "AvoidUsingCmdletAliases");
    }

    [Fact]
    public async Task AnalyzeScript_DiagnosticContainsExtent()
    {
        var response = await _client.AnalyzeScriptAsync(new AnalyzeScriptRequest
        {
            ScriptContent = "gps",
        });

        Assert.NotEmpty(response.Diagnostics);
        var diag = response.Diagnostics[0];
        Assert.NotNull(diag.Extent);
        Assert.Equal(1, diag.Extent.StartLine);
        Assert.True(diag.Extent.EndColumn > diag.Extent.StartColumn);
    }

    [Fact]
    public async Task AnalyzeScript_WithFilePath_ReturnsResults()
    {
        var response = await _client.AnalyzeScriptAsync(new AnalyzeScriptRequest
        {
            ScriptContent = "gps",
            FilePath = "/tmp/test.ps1",
        });

        Assert.NotEmpty(response.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeScript_EmptyScript_ReturnsNoDiagnostics()
    {
        var response = await _client.AnalyzeScriptAsync(new AnalyzeScriptRequest
        {
            ScriptContent = "",
        });

        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeFile_TempFile_ReturnsDiagnostics()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "gps");
            string ps1Path = Path.ChangeExtension(tempFile, ".ps1");
            File.Move(tempFile, ps1Path);
            tempFile = ps1Path;

            var response = await _client.AnalyzeFileAsync(new AnalyzeFileRequest
            {
                FilePath = tempFile,
            });

            Assert.NotEmpty(response.Diagnostics);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task FormatScript_AlreadyFormatted_ReturnsUnchanged()
    {
        var response = await _client.FormatScriptAsync(new FormatScriptRequest
        {
            ScriptContent = "Get-Process",
        });

        Assert.Equal("Get-Process", response.FormattedContent);
        Assert.False(response.Changed);
    }

    [Fact]
    public async Task FormatScript_WithPreset_Succeeds()
    {
        var request = new FormatScriptRequest
        {
            ScriptContent = "Get-Process",
        };
        request.Settings.Add("preset", "OTBS");

        var response = await _client.FormatScriptAsync(request);

        Assert.False(string.IsNullOrEmpty(response.FormattedContent));
    }

    [Fact]
    public async Task GetRules_ReturnsNonEmptyList()
    {
        var response = await _client.GetRulesAsync(new GetRulesRequest());

        Assert.NotEmpty(response.Rules);
    }

    [Fact]
    public async Task GetRules_RulesHaveRequiredFields()
    {
        var response = await _client.GetRulesAsync(new GetRulesRequest());

        foreach (var rule in response.Rules)
        {
            Assert.False(string.IsNullOrEmpty(rule.Name), $"Rule has empty Name");
            Assert.False(string.IsNullOrEmpty(rule.Id), $"Rule {rule.Name} has empty Id");
            Assert.NotEqual(Grpc.DiagnosticSeverity.Unspecified, rule.DefaultSeverity);
        }
    }

    [Fact]
    public async Task GetRules_ContainsAvoidCmdletAliases()
    {
        var response = await _client.GetRulesAsync(new GetRulesRequest());

        Assert.Contains(response.Rules, r => r.Name == "AvoidUsingCmdletAliases");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var handler = new SocketsHttpHandler();
        using var httpClient = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.BaseAddress}/health")
        {
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.True(body.Rules > 0);
    }

    private sealed record HealthResponse(string Status, int Rules);
}
