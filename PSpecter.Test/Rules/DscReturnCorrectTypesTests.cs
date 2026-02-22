using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin.Rules.Dsc;
using PSpecter.Execution;
using Xunit;
using Xunit.Abstractions;

namespace PSpecter.Test.Rules
{
    public class DscReturnCorrectTypesTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;
        private readonly ITestOutputHelper _output;

        public DscReturnCorrectTypesTests(ITestOutputHelper output)
        {
            _output = output;
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<ReturnCorrectTypesForDscFunctions>())
                .Build();
        }

        [Fact]
        public void PipelineOutputAnalyzer_InfersTypesThroughAssignments()
        {
            string script = @"
function Test-TargetResource {
    param([string]$Name)
    $b = @{Test=3}
    $b
    return $true
}
";
            var ast = Parser.ParseInput(script, out Token[] tokens, out ParseError[] errors);
            var funcs = DscResourceHelper.GetDscResourceFunctions(ast);
            Assert.Single(funcs);
            var outputs = PipelineOutputAnalyzer.GetOutputs(funcs[0]);
            Assert.Equal(2, outputs.Count);
            Assert.Equal("System.Collections.Hashtable", outputs[0].TypeName);
            Assert.Equal("System.Boolean", outputs[1].TypeName);
        }

        [Fact]
        public void TestTargetResource_WrongType_ProducesViolation()
        {
            string script = @"
function Set-TargetResource { param([string]$Name) }
function Get-TargetResource { param([string]$Name) return @{} }
function Test-TargetResource {
    param([string]$Name)
    $b = @{Test=3}
    $b
    return $true
}
";
            var diagnostics = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();
            Assert.Single(diagnostics);
            Assert.Contains("Test-TargetResource", diagnostics[0].Message);
            Assert.Contains("System.Boolean", diagnostics[0].Message);
        }

        [Fact]
        public void WaitForAll_ShouldReturn5Violations()
        {
            string filePath = Path.GetFullPath("../../../../Tests/Rules/DSCResourceModule/DSCResources/MSFT_WaitForAll/MSFT_WaitForAll.psm1");
            var diagnostics = _scriptAnalyzer.AnalyzeScriptPath(filePath).ToList();
            Assert.Equal(5, diagnostics.Count);
        }

        [Fact]
        public void WaitForAny_ShouldReturnNoViolations()
        {
            string filePath = Path.GetFullPath("../../../../Tests/Rules/DSCResourceModule/DSCResources/MSFT_WaitForAny/MSFT_WaitForAny.psm1");
            var diagnostics = _scriptAnalyzer.AnalyzeScriptPath(filePath).ToList();
            Assert.Empty(diagnostics);
        }
    }
}
