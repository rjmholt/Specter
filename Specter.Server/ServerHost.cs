using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Specter.Server;

public enum ServerMode
{
    Lsp,
    Grpc,
}

public sealed class ServerHostOptions
{
    public ServerMode Mode { get; set; } = ServerMode.Lsp;
    public int GrpcPort { get; set; } = 50051;
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public string? ConfigPath { get; set; }
}

public static class ServerHost
{
    public static async Task RunLspAsync(ServerHostOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ServerHostOptions();
        using AnalysisService service = AnalysisService.CreateFromConfig(options.ConfigPath);
        var server = await LspServer.CreateAsync(
            service,
            Console.OpenStandardInput(),
            Console.OpenStandardOutput(),
            cancellationToken);

        await server.WaitForExit;
    }

    public static async Task RunGrpcAsync(ServerHostOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ServerHostOptions();
        using AnalysisService service = AnalysisService.CreateFromConfig(options.ConfigPath);

        WebApplication app = BuildGrpcApplication(service, options.GrpcPort, options.LogLevel);
        await app.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="WebApplication"/> configured for gRPC with the health endpoint.
    /// Exposed for integration testing.
    /// </summary>
    public static WebApplication BuildGrpcApplication(AnalysisService service, int port = 50051, LogLevel logLevel = LogLevel.Information)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(service);
        builder.Services.AddGrpc();
        builder.Logging.SetMinimumLevel(logLevel);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        WebApplication app = builder.Build();
        app.MapGrpcService<GrpcAnalysisService>();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", rules = service.GetRules().Count }));

        return app;
    }
}
