using Microsoft.Extensions.Logging;
using PSpecter.Server;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "lsp":
        await ServerHost.RunLspAsync();
        return 0;

    case "grpc":
        var options = ParseGrpcOptions(args);
        await ServerHost.RunGrpcAsync(options);
        return 0;

    case "--help":
    case "-h":
    case "help":
        PrintUsage();
        return 0;

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: pspecter-server <command> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  lsp     Start the LSP server (communicates over stdin/stdout)");
    Console.Error.WriteLine("  grpc    Start the gRPC server");
    Console.Error.WriteLine();
    Console.Error.WriteLine("gRPC Options:");
    Console.Error.WriteLine("  --port <port>     Port to listen on (default: 50051)");
    Console.Error.WriteLine("  --log-level <lvl> Log level: Trace, Debug, Information, Warning, Error (default: Information)");
}

static ServerHostOptions ParseGrpcOptions(string[] args)
{
    var options = new ServerHostOptions { Mode = ServerMode.Grpc };
    for (int i = 1; i < args.Length - 1; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--port":
                if (int.TryParse(args[++i], out int port))
                {
                    options.GrpcPort = port;
                }
                break;

            case "--log-level":
                if (Enum.TryParse<LogLevel>(args[++i], ignoreCase: true, out var level))
                {
                    options.LogLevel = level;
                }
                break;
        }
    }
    return options;
}
