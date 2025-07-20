using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Xunit;
using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules;

namespace ScriptAnalyzer2.Test.Rules
{
    public class AvoidUsingWMICmdletTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingWMICmdletTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUsingWMICmdlet>())
                .Build();
        }

        [Fact]
        public void GetWmiObject_ShouldReturnViolation()
        {
            var script = @"Get-WmiObject -Class Win32_ComputerSystem";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(DiagnosticSeverity.Warning, oneViolation.Severity);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(42, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void InvokeWmiMethod_ShouldReturnViolation()
        {
            var script = @"Invoke-WmiMethod -Path Win32_Process -Name Create -ArgumentList notepad.exe";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(76, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void RegisterWmiEvent_ShouldReturnViolation()
        {
            var script = @"Register-WmiEvent -Class Win32_ProcessStartTrace -SourceIdentifier 'ProcessStarted'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(84, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void SetWmiInstance_ShouldReturnViolation()
        {
            var script = @"Set-WmiInstance -Class Win32_Environment -Argument @{Name='MyEnvVar';VariableValue='VarValue'}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(95, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void RemoveWmiObject_ShouldReturnViolation()
        {
            var script = @"Remove-WmiObject -Class Win32_OperatingSystem -Verbose";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(55, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void MultipleWmiCmdlets_ShouldReturnMultipleViolations()
        {
            var script = @"
Get-WmiObject -Class Win32_ComputerSystem
Invoke-WmiMethod -Path Win32_Process -Name Create
Register-WmiEvent -Class Win32_ProcessStartTrace";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(3, violations.Count);

            // First violation - Get-WmiObject
            Assert.Equal(2, violations[0].ScriptExtent.StartLineNumber);
            Assert.Equal(2, violations[0].ScriptExtent.EndLineNumber);
            Assert.Equal(1, violations[0].ScriptExtent.StartColumnNumber);
            Assert.Equal(42, violations[0].ScriptExtent.EndColumnNumber);

            // Second violation - Invoke-WmiMethod
            Assert.Equal(3, violations[1].ScriptExtent.StartLineNumber);
            Assert.Equal(3, violations[1].ScriptExtent.EndLineNumber);
            Assert.Equal(1, violations[1].ScriptExtent.StartColumnNumber);
            Assert.Equal(50, violations[1].ScriptExtent.EndColumnNumber);

            // Third violation - Register-WmiEvent
            Assert.Equal(4, violations[2].ScriptExtent.StartLineNumber);
            Assert.Equal(4, violations[2].ScriptExtent.EndLineNumber);
            Assert.Equal(1, violations[2].ScriptExtent.StartColumnNumber);
            Assert.Equal(49, violations[2].ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void WmiCmdletInFunction_ShouldReturnViolation()
        {
            var script = @"
function Get-ComputerInfo {
    Get-WmiObject -Class Win32_ComputerSystem
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(3, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(3, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(5, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(46, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void WmiCmdletInScriptBlock_ShouldReturnViolation()
        {
            var script = @"
Invoke-Command -ScriptBlock {
    Get-WmiObject -Class Win32_Service
}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal(3, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(3, oneViolation.ScriptExtent.EndLineNumber);
            Assert.Equal(5, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(39, oneViolation.ScriptExtent.EndColumnNumber);
        }

        [Fact]
        public void CimCmdlets_ShouldNotReturnViolation()
        {
            var script = @"
Get-CimInstance -ClassName Win32_ComputerSystem
Invoke-CimMethod -ClassName Win32_Process -MethodName Create
Register-CimIndicationEvent -ClassName Win32_ProcessStartTrace
Set-CimInstance -ClassName Win32_Environment
Remove-CimInstance -ClassName Win32_Service";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void WmiCmdletCaseInsensitive_ShouldReturnViolation()
        {
            var script = @"
get-wmiobject -Class Win32_ComputerSystem
INVOKE-WMIMETHOD -Path Win32_Process
Register-WMIEvent -Class Win32_ProcessStartTrace";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(3, violations.Count);
            Assert.All(violations, v => Assert.Equal("AvoidUsingWMICmdlet", v.Rule.Name));
        }

        [Fact]
        public void PowerShellVersion2_ShouldNotReturnViolation()
        {
            var script = @"
#requires -Version 2.0
Get-WmiObject -Class Win32_ComputerSystem";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PowerShellVersion3AndAbove_ShouldReturnViolation()
        {
            var script = @"
#requires -Version 3.0
Get-WmiObject -Class Win32_ComputerSystem";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(3, oneViolation.ScriptExtent.StartLineNumber);
        }

        [Fact]
        public void NonWmiCmdlets_ShouldNotReturnViolation()
        {
            var script = @"
Get-Process
Get-Service
Invoke-Command
Register-EngineEvent
Set-Variable";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void WmiCmdletScriptExtent_ShouldCoverWholeCommand()
        {
            var script = @"Get-WmiObject -Class Win32_ComputerSystem";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("Get-WmiObject -Class Win32_ComputerSystem", oneViolation.ScriptExtent.Text);
        }

        [Fact]
        public void WmiCmdletWithPipeline_ShouldReturnViolation()
        {
            var script = @"Get-WmiObject -Class Win32_Process | Where-Object {$_.Name -eq 'notepad'}";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic oneViolation = Assert.Single(violations);
            Assert.Equal("AvoidUsingWMICmdlet", oneViolation.Rule.Name);
            Assert.Equal(1, oneViolation.ScriptExtent.StartLineNumber);
            Assert.Equal(1, oneViolation.ScriptExtent.StartColumnNumber);
            Assert.Equal(35, oneViolation.ScriptExtent.EndColumnNumber);
        }
    }
}