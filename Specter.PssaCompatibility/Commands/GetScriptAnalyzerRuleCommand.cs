using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Specter;
using Specter.Builder;
using Specter.Module;
using Specter.Configuration;
using Specter.Execution;
using Specter.Instantiation;
using Specter.Rules;
using Specter.Security;
using CompatSeverity = Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticSeverity;
using EngineSeverity = Specter.DiagnosticSeverity;

namespace Specter.PssaCompatibility.Commands
{
    [Cmdlet(VerbsCommon.Get, "ScriptAnalyzerRule")]
    public class GetScriptAnalyzerRuleCommand : PSCmdlet
    {
        [Parameter(Position = 0)]
        [ValidateNotNull]
        public string[]? Name { get; set; }

        [ValidateSet("Warning", "Error", "Information", "ParseError", IgnoreCase = true)]
        [Parameter]
        public string[]? Severity { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[]? CustomRulePath { get; set; }

        [Parameter]
        public SwitchParameter RecurseCustomRulePath { get; set; }

        protected override void ProcessRecord()
        {
            var configDict = new Dictionary<string, IRuleConfiguration?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in Specter.Rules.Builtin.Default.RuleConfiguration)
            {
                if (kvp.Value is not null)
                {
                    configDict[kvp.Key] = kvp.Value;
                }
            }

            var builder = new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(rcpb => rcpb.UseSessionDatabase(SessionState.InvokeCommand))
                .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
                .AddBuiltinRules();

            if (CustomRulePath is not null)
            {
                foreach (string rulePath in CustomRulePath)
                {
                    string resolvedPath = Path.IsPathRooted(rulePath)
                        ? rulePath
                        : Path.GetFullPath(rulePath);

                    var factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                        resolvedPath,
                        settingsFileDirectory: null,
                        configDict,
                        RecurseCustomRulePath.IsPresent,
                        skipOwnershipCheck: false,
                        logger: null);

                    foreach (var factory in factories)
                    {
                        builder.AddRuleProviderFactory(factory);
                    }
                }
            }

            ScriptAnalyzer analyzer = builder.Build();

            HashSet<string>? severityFilter = null;
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
            Specter.Rules.RuleInfo ruleInfo,
            string pssaRuleName,
            DiagnosticSeverity severity)
        {
            RuleName = pssaRuleName;
            CommonName = ruleInfo.Name;
            Description = ruleInfo.Description ?? string.Empty;
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
