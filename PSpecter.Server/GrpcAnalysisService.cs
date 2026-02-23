using Grpc.Core;
using PSpecter.Rules;
using PSpecter.Server.Grpc;

namespace PSpecter.Server;

public sealed class GrpcAnalysisService : PSpecterAnalysis.PSpecterAnalysisBase
{
    private readonly AnalysisService _analysisService;

    public GrpcAnalysisService(AnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    public override Task<AnalyzeScriptResponse> AnalyzeScript(AnalyzeScriptRequest request, ServerCallContext context)
    {
        IReadOnlyCollection<ScriptDiagnostic> diagnostics = _analysisService.AnalyzeScriptContent(
            request.ScriptContent,
            string.IsNullOrEmpty(request.FilePath) ? null : request.FilePath);

        return Task.FromResult(MapToResponse(diagnostics));
    }

    public override Task<AnalyzeScriptResponse> AnalyzeFile(AnalyzeFileRequest request, ServerCallContext context)
    {
        IReadOnlyCollection<ScriptDiagnostic> diagnostics = _analysisService.AnalyzeFile(request.FilePath);
        return Task.FromResult(MapToResponse(diagnostics));
    }

    public override Task<GetRulesResponse> GetRules(GetRulesRequest request, ServerCallContext context)
    {
        IReadOnlyList<RuleInfo> rules = _analysisService.GetRules();
        var response = new GetRulesResponse();
        foreach (RuleInfo rule in rules)
        {
            response.Rules.Add(new RuleDescriptor
            {
                Name = rule.Name,
                Id = rule.FullName,
                Description = rule.Description ?? string.Empty,
                DefaultSeverity = MapSeverity(rule.DefaultSeverity),
            });
        }
        return Task.FromResult(response);
    }

    public override Task<FormatScriptResponse> FormatScript(FormatScriptRequest request, ServerCallContext context)
    {
        request.Settings.TryGetValue("preset", out string? preset);

        FormatResult result = _analysisService.FormatScript(
            request.ScriptContent,
            string.IsNullOrEmpty(request.FilePath) ? null : request.FilePath,
            preset);

        return Task.FromResult(new FormatScriptResponse
        {
            FormattedContent = result.FormattedContent,
            Changed = result.Changed,
        });
    }

    private static AnalyzeScriptResponse MapToResponse(IReadOnlyCollection<ScriptDiagnostic> diagnostics)
    {
        var response = new AnalyzeScriptResponse();
        foreach (ScriptDiagnostic diag in diagnostics)
        {
            var grpcDiag = new Diagnostic
            {
                RuleName = diag.Rule?.Name ?? string.Empty,
                RuleId = diag.Rule?.FullName ?? string.Empty,
                Severity = MapSeverity(diag.Severity),
                Message = diag.Message,
            };

            if (diag.ScriptExtent is not null)
            {
                grpcDiag.Extent = new Grpc.ScriptExtent
                {
                    File = diag.ScriptExtent.File ?? string.Empty,
                    StartLine = diag.ScriptExtent.StartLineNumber,
                    StartColumn = diag.ScriptExtent.StartColumnNumber,
                    EndLine = diag.ScriptExtent.EndLineNumber,
                    EndColumn = diag.ScriptExtent.EndColumnNumber,
                    StartOffset = diag.ScriptExtent.StartOffset,
                    EndOffset = diag.ScriptExtent.EndOffset,
                };
            }

            if (diag.Corrections is not null)
            {
                foreach (PSpecter.Correction correction in diag.Corrections)
                {
                    var grpcCorrection = new Grpc.Correction
                    {
                        Description = correction.Description ?? string.Empty,
                        ReplacementText = correction.CorrectionText ?? string.Empty,
                    };

                    if (correction.Extent is not null)
                    {
                        grpcCorrection.Extent = new Grpc.ScriptExtent
                        {
                            StartLine = correction.Extent.StartLineNumber,
                            StartColumn = correction.Extent.StartColumnNumber,
                            EndLine = correction.Extent.EndLineNumber,
                            EndColumn = correction.Extent.EndColumnNumber,
                        };
                    }

                    grpcDiag.Corrections.Add(grpcCorrection);
                }
            }

            response.Diagnostics.Add(grpcDiag);
        }
        return response;
    }

    private static Grpc.DiagnosticSeverity MapSeverity(PSpecter.DiagnosticSeverity severity)
    {
        return severity switch
        {
            PSpecter.DiagnosticSeverity.Information => Grpc.DiagnosticSeverity.Information,
            PSpecter.DiagnosticSeverity.Warning => Grpc.DiagnosticSeverity.Warning,
            PSpecter.DiagnosticSeverity.Error => Grpc.DiagnosticSeverity.Error,
            _ => Grpc.DiagnosticSeverity.Unspecified,
        };
    }
}
