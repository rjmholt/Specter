using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using LspDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace PSpecter.Server;

public static class LspServer
{
    public static async Task<ILanguageServer> CreateAsync(
        AnalysisService analysisService,
        Stream input,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ILanguageServer server = await LanguageServer.From(options =>
        {
            options
                .WithInput(input)
                .WithOutput(output)
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithServices(services =>
                {
                    services.AddSingleton(analysisService);
                })
                .WithHandler<TextDocumentSyncHandler>();
        }, cancellationToken);

        return server;
    }
}

internal sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly AnalysisService _analysisService;
    private readonly ILanguageServerFacade _server;

    public TextDocumentSyncHandler(AnalysisService analysisService, ILanguageServerFacade server)
    {
        _analysisService = analysisService;
        _server = server;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "powershell");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.ps1" },
                new TextDocumentFilter { Pattern = "**/*.psm1" },
                new TextDocumentFilter { Pattern = "**/*.psd1" }),
            Change = OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities.TextDocumentSyncKind.Full,
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        string uri = request.TextDocument.Uri.ToString();
        string content = request.TextDocument.Text;
        int version = request.TextDocument.Version ?? 0;

        _analysisService.OpenDocument(uri, content, version);
        PublishDiagnostics(request.TextDocument.Uri, content);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        string uri = request.TextDocument.Uri.ToString();
        string? content = request.ContentChanges.FirstOrDefault()?.Text;
        if (content is null)
        {
            return Unit.Task;
        }

        int version = request.TextDocument.Version ?? 0;
        _analysisService.UpdateDocument(uri, content, version);
        PublishDiagnostics(request.TextDocument.Uri, content);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        string uri = request.TextDocument.Uri.ToString();
        _analysisService.CloseDocument(uri);

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<LspDiagnostic>(),
        });

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    private void PublishDiagnostics(DocumentUri documentUri, string content)
    {
        IReadOnlyCollection<ScriptDiagnostic> results = _analysisService.AnalyzeScriptContent(
            content, documentUri.ToString());

        var diagnostics = new List<LspDiagnostic>(results.Count);
        foreach (ScriptDiagnostic result in results)
        {
            if (result.ScriptExtent is null)
            {
                continue;
            }

            diagnostics.Add(new LspDiagnostic
            {
                Range = new Range(
                    new Position(result.ScriptExtent.StartLineNumber - 1, result.ScriptExtent.StartColumnNumber - 1),
                    new Position(result.ScriptExtent.EndLineNumber - 1, result.ScriptExtent.EndColumnNumber - 1)),
                Severity = MapSeverity(result.Severity),
                Code = result.Rule?.Name ?? string.Empty,
                Source = "PSpecter",
                Message = result.Message,
            });
        }

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = documentUri,
            Diagnostics = new Container<LspDiagnostic>(diagnostics),
        });
    }

    private static LspDiagnosticSeverity MapSeverity(PSpecter.DiagnosticSeverity severity)
    {
        return severity switch
        {
            PSpecter.DiagnosticSeverity.Error => LspDiagnosticSeverity.Error,
            PSpecter.DiagnosticSeverity.Warning => LspDiagnosticSeverity.Warning,
            PSpecter.DiagnosticSeverity.Information => LspDiagnosticSeverity.Information,
            _ => LspDiagnosticSeverity.Hint,
        };
    }
}
