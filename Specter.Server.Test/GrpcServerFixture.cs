using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Specter.Server;
using Xunit;

namespace Specter.Server.Test;

/// <summary>
/// xUnit fixture that starts a real gRPC server on an auto-assigned port
/// and provides a <see cref="GrpcChannel"/> for integration tests.
/// </summary>
public sealed class GrpcServerFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private AnalysisService? _service;

    public GrpcChannel Channel { get; private set; } = null!;

    public string BaseAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        _service = AnalysisService.CreateDefault();

        _app = ServerHost.BuildGrpcApplication(
            _service,
            port: 0,
            logLevel: LogLevel.Warning);

        await _app.StartAsync();

        string address = _app.Urls.First();
        BaseAddress = address;

        Channel = GrpcChannel.ForAddress(address);
    }

    public async Task DisposeAsync()
    {
        Channel?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        _service?.Dispose();
    }
}
