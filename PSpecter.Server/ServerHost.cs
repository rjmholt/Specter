using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PSpecter.Server;

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
}

public static class ServerHost
{
    public static async Task RunLspAsync(CancellationToken cancellationToken = default)
    {
        using AnalysisService service = AnalysisService.CreateDefault();
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
        using AnalysisService service = AnalysisService.CreateDefault();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(service);
        builder.Services.AddGrpc();
        builder.Logging.SetMinimumLevel(options.LogLevel);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenLocalhost(options.GrpcPort, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        WebApplication app = builder.Build();
        app.MapGrpcService<GrpcAnalysisService>();

        await app.RunAsync(cancellationToken);
    }
}
