using System;
using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Configuration;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Microsoft.PowerShell.ScriptAnalyzer.Instantiation;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Runtime;
using CompatSeverity = Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticSeverity;
using EngineSeverity = Microsoft.PowerShell.ScriptAnalyzer.DiagnosticSeverity;

namespace PSLint.PssaCompatibility.Commands
{
    [Cmdlet(VerbsCommon.Get, "ScriptAnalyzerRule")]
    public class GetScriptAnalyzerRuleCommand : PSCmdlet
    {
        [Parameter(Position = 0)]
        [ValidateNotNull]
        public string[] Name { get; set; }

        [ValidateSet("Warning", "Error", "Information", "ParseError", IgnoreCase = true)]
        [Parameter]
        public string[] Severity { get; set; }

        protected override void ProcessRecord()
        {
            IPowerShellCommandDatabase commandDb = SessionStateCommandDatabase.Create(
                SessionState.InvokeCommand);

            ScriptAnalyzer analyzer = new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(rcpb => rcpb.AddSingleton(commandDb))
                .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
                .AddBuiltinRules()
                .Build();

            HashSet<string> severityFilter = null;
            if (Severity is { Length: > 0 })
            {
                severityFilter = new HashSet<string>(Severity, StringComparer.OrdinalIgnoreCase);
            }

            foreach (IRuleProvider ruleProvider in analyzer.RuleProviders)
            {
                foreach (RuleInfo ruleInfo in ruleProvider.GetRuleInfos())
                {
                    string pssaName = RuleNameMapper.ToPssaRuleName(ruleInfo.FullName);

                    if (!PassesNameFilter(pssaName))
                    {
                        continue;
                    }

                    CompatSeverity severity = SeverityMapper.ToCompat(ruleInfo.DefaultSeverity);

                    if (severityFilter != null && !severityFilter.Contains(severity.ToString()))
                    {
                        continue;
                    }

                    WriteObject(new PSObject(new Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.RuleInfoRecord(ruleInfo, pssaName, severity)));
                }
            }
        }

        private bool PassesNameFilter(string pssaName)
        {
            if (Name is null || Name.Length == 0)
            {
                return true;
            }

            foreach (string pattern in Name)
            {
                if (RuleNameMapper.IsMatch(pattern, pssaName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    /// <summary>
    /// Compatibility record wrapping RuleInfo with PSSA-compatible property names.
    /// </summary>
    public class RuleInfoRecord
    {
        internal RuleInfoRecord(
            Microsoft.PowerShell.ScriptAnalyzer.Rules.RuleInfo ruleInfo,
            string pssaRuleName,
            DiagnosticSeverity severity)
        {
            RuleName = pssaRuleName;
            CommonName = ruleInfo.Name;
            Description = ruleInfo.Description;
            SourceType = ruleInfo.Source.ToString();
            Severity = severity;
        }

        public string RuleName { get; }
        public string CommonName { get; }
        public string Description { get; }
        public string SourceType { get; }
        public DiagnosticSeverity Severity { get; }

        public override string ToString() => RuleName;
    }
}
