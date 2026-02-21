using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Configuration;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Microsoft.PowerShell.ScriptAnalyzer.Runtime;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;

#if !CORECLR
using Microsoft.PowerShell.ScriptAnalyzer.Internal;
#endif

namespace PSLint.PssaCompatibility.Commands
{
    [Cmdlet(VerbsLifecycle.Invoke, "ScriptAnalyzer", DefaultParameterSetName = "File")]
    [OutputType(typeof(DiagnosticRecord))]
    public class InvokeScriptAnalyzerCommand : PSCmdlet
    {
        private ScriptAnalyzer _scriptAnalyzer;

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "File",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        [Alias("PSPath")]
        public string Path { get; set; }

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "ScriptDefinition",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public string ScriptDefinition { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[] IncludeRule { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[] ExcludeRule { get; set; }

        [ValidateSet("Warning", "Error", "Information", "ParseError", IgnoreCase = true)]
        [Parameter]
        public string[] Severity { get; set; }

        [Parameter]
        [ValidateNotNull]
        public object Settings { get; set; }

        [Parameter]
        public SwitchParameter Recurse { get; set; }

        [Parameter]
        public SwitchParameter Fix { get; set; }

        [Parameter]
        public SwitchParameter EnableExit { get; set; }

        [Parameter]
        public SwitchParameter ReportSummary { get; set; }

        [Parameter]
        public SwitchParameter SuppressedOnly { get; set; }

        protected override void BeginProcessing()
        {
            _scriptAnalyzer = BuildAnalyzer();
        }

        protected override void ProcessRecord()
        {
            IReadOnlyCollection<ScriptDiagnostic> diagnostics;

            try
            {
                if (ParameterSetName == "File")
                {
                    string resolvedPath = ResolvePath(Path);
                    if (resolvedPath == null)
                    {
                        return;
                    }

                    diagnostics = _scriptAnalyzer.AnalyzeScriptPath(resolvedPath);
                }
                else
                {
                    diagnostics = _scriptAnalyzer.AnalyzeScriptInput(ScriptDefinition);
                }
            }
            catch (AggregateException ae)
            {
                foreach (Exception inner in ae.Flatten().InnerExceptions)
                {
                    WriteError(new ErrorRecord(
                        inner,
                        "RuleExecutionError",
                        ErrorCategory.InvalidOperation,
                        null));
                }
                return;
            }

            HashSet<string> severityFilter = BuildSeverityFilter();

            foreach (ScriptDiagnostic diagnostic in diagnostics)
            {
                DiagnosticRecord record = DiagnosticRecord.FromEngineDiagnostic(diagnostic);

                if (!PassesIncludeFilter(record.RuleName))
                {
                    continue;
                }

                if (IsExcluded(record.RuleName))
                {
                    continue;
                }

                if (severityFilter != null && !severityFilter.Contains(record.Severity.ToString()))
                {
                    continue;
                }

                WriteObject(record);
            }
        }

        private string ResolvePath(string inputPath)
        {
            try
            {
                var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(inputPath);
                if (resolved.Count > 0)
                {
                    return resolved[0].Path;
                }
            }
            catch (ItemNotFoundException)
            {
                WriteError(new ErrorRecord(
                    new FileNotFoundException($"Cannot find path '{inputPath}' because it does not exist.", inputPath),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    inputPath));
            }

            return null;
        }

        private bool PassesIncludeFilter(string ruleName)
        {
            if (IncludeRule is null || IncludeRule.Length == 0)
            {
                return true;
            }

            foreach (string pattern in IncludeRule)
            {
                if (RuleNameMapper.IsMatch(pattern, ruleName))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsExcluded(string ruleName)
        {
            if (ExcludeRule is null || ExcludeRule.Length == 0)
            {
                return false;
            }

            foreach (string pattern in ExcludeRule)
            {
                if (RuleNameMapper.IsMatch(pattern, ruleName))
                {
                    return true;
                }
            }

            return false;
        }

        private HashSet<string> BuildSeverityFilter()
        {
            if (Severity is null || Severity.Length == 0)
            {
                return null;
            }

            return new HashSet<string>(Severity, StringComparer.OrdinalIgnoreCase);
        }

        private ScriptAnalyzer BuildAnalyzer()
        {
            IPowerShellCommandDatabase commandDb = SessionStateCommandDatabase.Create(
                SessionState.InvokeCommand);

            return new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(rcpb => rcpb.AddSingleton(commandDb))
                .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
                .AddBuiltinRules()
                .Build();
        }
    }
}
