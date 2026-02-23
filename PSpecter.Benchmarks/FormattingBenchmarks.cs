using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PSpecter.Builtin;
using PSpecter.Formatting;

namespace PSpecter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class FormattingBenchmarks
{
    private ScriptFormatter _defaultFormatter = null!;
    private ScriptFormatter _otbsFormatter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _defaultFormatter = ScriptFormatter.FromEditorConfigs(FormatterPresets.Default);
        _otbsFormatter = ScriptFormatter.FromEditorConfigs(FormatterPresets.OTBS);
    }

    [Benchmark(Baseline = true)]
    public string FormatSmall_Default()
    {
        return _defaultFormatter.Format(SmallScript);
    }

    [Benchmark]
    public string FormatSmall_OTBS()
    {
        return _otbsFormatter.Format(SmallScript);
    }

    [Benchmark]
    public string FormatMedium_Default()
    {
        return _defaultFormatter.Format(MediumScript);
    }

    private const string SmallScript = @"
function Get-Greeting{
param(  [string]$Name )
Write-Host ""Hello, $Name""
}
";

    private const string MediumScript = @"
function Get-ServerStatus{
[CmdletBinding()]
param(
[Parameter(Mandatory)]
[string[]]$ComputerName,
[ValidateSet('Quick','Full')]
[string]$Mode='Quick'
)

begin{
$results=[System.Collections.Generic.List[pscustomobject]]::new()
}

process{
foreach($computer in $ComputerName){
try{
$session=New-PSSession -ComputerName $computer -ErrorAction Stop
$info=Invoke-Command -Session $session -ScriptBlock {
[pscustomobject]@{
Hostname=$env:COMPUTERNAME
OS=(Get-CimInstance Win32_OperatingSystem).Caption
}
}
$results.Add($info)
}catch{
Write-Warning ""Failed: $_""
}finally{
if($session){Remove-PSSession $session}
}
}
}

end{
$results
}
}
";
}
