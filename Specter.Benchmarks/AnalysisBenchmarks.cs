using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Specter.Builder;
using Specter.Builtin;
using Specter.Execution;
using System.Management.Automation.Language;

namespace Specter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class AnalysisBenchmarks
{
    private ScriptAnalyzer _parallelAnalyzer = null!;
    private ScriptAnalyzer _sequentialAnalyzer = null!;

    private Ast _smallAst = null!;
    private Token[] _smallTokens = null!;

    private Ast _mediumAst = null!;
    private Token[] _mediumTokens = null!;

    private Ast _largeAst = null!;
    private Token[] _largeTokens = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parallelAnalyzer = new ScriptAnalyzerBuilder()
            .WithRuleComponentProvider(b => b.UseBuiltinDatabase())
            .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
            .AddBuiltinRules()
            .Build();

        _sequentialAnalyzer = new ScriptAnalyzerBuilder()
            .WithRuleComponentProvider(b => b.UseBuiltinDatabase())
            .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
            .AddBuiltinRules()
            .Build();

        Parser.ParseInput(SmallScript, out _smallTokens, out _);
        _smallAst = Parser.ParseInput(SmallScript, out _smallTokens, out _);

        _mediumAst = Parser.ParseInput(MediumScript, out _mediumTokens, out _);

        _largeAst = Parser.ParseInput(LargeScript, out _largeTokens, out _);
    }

    [Benchmark(Baseline = true)]
    public int SmallScript_Parallel()
    {
        return _parallelAnalyzer.AnalyzeScript(_smallAst, _smallTokens).Count;
    }

    [Benchmark]
    public int SmallScript_Sequential()
    {
        return _sequentialAnalyzer.AnalyzeScript(_smallAst, _smallTokens).Count;
    }

    [Benchmark]
    public int MediumScript_Parallel()
    {
        return _parallelAnalyzer.AnalyzeScript(_mediumAst, _mediumTokens).Count;
    }

    [Benchmark]
    public int MediumScript_Sequential()
    {
        return _sequentialAnalyzer.AnalyzeScript(_mediumAst, _mediumTokens).Count;
    }

    [Benchmark]
    public int LargeScript_Parallel()
    {
        return _parallelAnalyzer.AnalyzeScript(_largeAst, _largeTokens).Count;
    }

    [Benchmark]
    public int LargeScript_Sequential()
    {
        return _sequentialAnalyzer.AnalyzeScript(_largeAst, _largeTokens).Count;
    }

    [Benchmark]
    public int SmallScript_ParseAndAnalyze()
    {
        return _parallelAnalyzer.AnalyzeScriptInput(SmallScript).Count;
    }

    internal const string SmallScriptText = SmallScript;

    private const string SmallScript = @"
function Get-Greeting {
    param(
        [string]$Name
    )
    Write-Host ""Hello, $Name""
}
";

    private const string MediumScript = @"
function Get-ServerStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$ComputerName,

        [ValidateSet('Quick', 'Full')]
        [string]$Mode = 'Quick',

        [PSCredential]$Credential
    )

    begin {
        $results = [System.Collections.Generic.List[pscustomobject]]::new()
    }

    process {
        foreach ($computer in $ComputerName) {
            try {
                $session = New-PSSession -ComputerName $computer -Credential $Credential -ErrorAction Stop

                $info = Invoke-Command -Session $session -ScriptBlock {
                    [pscustomobject]@{
                        Hostname    = $env:COMPUTERNAME
                        OS          = (Get-CimInstance Win32_OperatingSystem).Caption
                        Uptime      = (Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
                        CPUPercent  = (Get-Counter '\Processor(_Total)\% Processor Time').CounterSamples.CookedValue
                        FreeMemGB   = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)
                    }
                }

                if ($Mode -eq 'Full') {
                    $diskInfo = Invoke-Command -Session $session -ScriptBlock {
                        Get-CimInstance Win32_LogicalDisk -Filter ""DriveType=3"" |
                            Select-Object DeviceID,
                                @{N='SizeGB';E={[math]::Round($_.Size/1GB,2)}},
                                @{N='FreeGB';E={[math]::Round($_.FreeSpace/1GB,2)}}
                    }
                    $info | Add-Member -NotePropertyName DiskInfo -NotePropertyValue $diskInfo
                }

                $results.Add($info)
            }
            catch {
                Write-Warning ""Failed to connect to $computer : $_""
            }
            finally {
                if ($session) { Remove-PSSession $session }
            }
        }
    }

    end {
        $results
    }
}

function Test-Connectivity {
    param([string]$Target)
    Test-Connection -ComputerName $Target -Count 1 -Quiet
}
";

    private static string LargeScript { get; } = GenerateLargeScript();

    private static string GenerateLargeScript()
    {
        var sb = new System.Text.StringBuilder(64 * 1024);
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($@"
function Get-Item{i} {{
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Path,

        [switch]$Force,

        [ValidateRange(1, 100)]
        [int]$Depth = 5
    )

    begin {{
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $processedCount = 0
    }}

    process {{
        if ($PSCmdlet.ShouldProcess($Path, 'Get item')) {{
            $item = Get-Item -Path $Path -ErrorAction SilentlyContinue
            if ($null -eq $item) {{
                Write-Error ""Item not found: $Path""
                return
            }}

            $processedCount++
            [pscustomobject]@{{
                Name      = $item.Name
                FullPath  = $item.FullName
                Size      = if ($item -is [System.IO.FileInfo]) {{ $item.Length }} else {{ 0 }}
                IsDir     = $item.PSIsContainer
                Modified  = $item.LastWriteTime
                Index     = {i}
            }}
        }}
    }}

    end {{
        $stopwatch.Stop()
        Write-Verbose ""Processed $processedCount items in $($stopwatch.Elapsed)""
    }}
}}
");
        }
        return sb.ToString();
    }
}
