using Microsoft.Extensions.Logging;
using Specter.Server;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "lsp":
        var lspOptions = ParseOptions(args, ServerMode.Lsp);
        await ServerHost.RunLspAsync(lspOptions);
        return 0;

    case "grpc":
        var options = ParseOptions(args, ServerMode.Grpc);
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
    Console.Error.WriteLine("Usage: specter-server <command> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  lsp     Start the LSP server (communicates over stdin/stdout)");
    Console.Error.WriteLine("  grpc    Start the gRPC server");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Common Options:");
    Console.Error.WriteLine("  --config <path>   Path to a Specter settings file (.psd1 or .json)");
    Console.Error.WriteLine("  --log-level <lvl> Log level: Trace, Debug, Information, Warning, Error (default: Information)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("gRPC Options:");
    Console.Error.WriteLine("  --port <port>     Port to listen on (default: 50051)");
}

static ServerHostOptions ParseOptions(string[] args, ServerMode mode)
{
    var options = new ServerHostOptions { Mode = mode };
    for (int i = 1; i < args.Length; i++)
    {
        if (i + 1 >= args.Length)
        {
            break;
        }

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

            case "--config":
                options.ConfigPath = args[++i];
                break;
        }
    }
    return options;
}
