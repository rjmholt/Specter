using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PSpecter.Suppression;
using System.Management.Automation.Language;

namespace PSpecter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ParsingBenchmarks
{
    private const string ScriptWithSuppressions = @"
[System.Diagnostics.CodeAnalysis.SuppressMessage('PSAvoidUsingWriteHost', '')]
[System.Diagnostics.CodeAnalysis.SuppressMessage('PSAvoidGlobalVars', '')]
param()

function Test-One {
    [System.Diagnostics.CodeAnalysis.SuppressMessage('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [System.Diagnostics.CodeAnalysis.SuppressMessage('PSAvoidDefaultValueForMandatoryParameter', '')]
        [Parameter(Mandatory)]
        [string]$Name = 'default'
    )
    Write-Host $Name
    $global:foo = 42
}
";

    private Ast _suppressionAst = null!;

    [GlobalSetup]
    public void Setup()
    {
        _suppressionAst = Parser.ParseInput(ScriptWithSuppressions, out _, out _);
    }

    [Benchmark]
    public int ParseSuppressions()
    {
        var suppressions = SuppressionParser.GetSuppressions(_suppressionAst);
        return suppressions.Count;
    }

    [Benchmark]
    public Ast ParseSmallScript()
    {
        return Parser.ParseInput(AnalysisBenchmarks.SmallScriptText, out _, out _);
    }
}
