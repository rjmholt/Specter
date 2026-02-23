using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Specter.CommandDatabase;

namespace Specter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class DatabaseBenchmarks
{
    private BuiltinCommandDatabase _db = null!;

    [GlobalSetup]
    public void Setup()
    {
        string? dbPath = BuiltinCommandDatabase.FindDefaultDatabasePath();
        if (dbPath is null)
        {
            throw new InvalidOperationException("specter.db not found; run build-database.ps1 first");
        }

        _db = BuiltinCommandDatabase.CreateWithDatabase(dbPath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool LookupKnownCommand()
    {
        return _db.TryGetCommand("Get-ChildItem", platforms: null, out _);
    }

    [Benchmark]
    public bool LookupUnknownCommand()
    {
        return _db.TryGetCommand("Get-NonExistentCmdlet", platforms: null, out _);
    }

    [Benchmark]
    public string? ResolveAlias()
    {
        return _db.GetAliasTarget("gci");
    }

    [Benchmark]
    public bool CommandExists()
    {
        return _db.CommandExistsOnPlatform("Invoke-WebRequest", platforms: null);
    }
}
