using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using Specter.Logging;
using Specter.Rules;
using Specter.Security;

namespace Specter.Instantiation
{
    /// <summary>
    /// Provides rules discovered from a PowerShell module loaded in a constrained runspace.
    /// Each provider owns its runspace and disposes it when no longer needed.
    /// </summary>
    internal sealed class PSModuleRuleProvider : IRuleProvider, IDisposable
    {
        private readonly string _modulePath;
        private readonly Runspace _runspace;
        private readonly IAnalysisLogger? _logger;
        private readonly List<DiscoveredPSRule> _discoveredRules;
        private readonly Dictionary<string, PSFunctionRule> _ruleInstances;

        internal PSModuleRuleProvider(
            string absoluteModulePath,
            Runspace runspace,
            IAnalysisLogger? logger)
        {
            _modulePath = absoluteModulePath;
            _runspace = runspace;
            _logger = logger;
            _discoveredRules = PSRuleDiscovery.DiscoverRules(runspace, logger);
            _ruleInstances = new Dictionary<string, PSFunctionRule>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<RuleInfo> GetRuleInfos()
        {
            for (int i = 0; i < _discoveredRules.Count; i++)
            {
                yield return _discoveredRules[i].RuleInfo;
            }
        }

        public IEnumerable<ScriptRule> GetScriptRules()
        {
            for (int i = 0; i < _discoveredRules.Count; i++)
            {
                DiscoveredPSRule discovered = _discoveredRules[i];
                string key = discovered.FunctionName;

                if (!_ruleInstances.TryGetValue(key, out PSFunctionRule? rule))
                {
                    rule = new PSFunctionRule(
                        discovered.RuleInfo,
                        _runspace,
                        discovered.FunctionName,
                        discovered.Convention,
                        _logger);
                    _ruleInstances[key] = rule;
                }

                yield return rule;
            }
        }

        public void ReturnRule(Rule rule)
        {
            // PSFunctionRules are reused (not pooled), nothing to return
        }

        public void Dispose()
        {
            foreach (var kvp in _ruleInstances)
            {
                kvp.Value.Dispose();
            }

            _ruleInstances.Clear();
            _runspace?.Dispose();
        }
    }

    /// <summary>
    /// Factory that creates a PSModuleRuleProvider by:
    /// 1. Creating a constrained runspace
    /// 2. Auditing the module manifest (if .psd1)
    /// 3. Importing the module
    /// 4. Locking down the runspace
    /// 5. Discovering rules
    /// </summary>
    internal sealed class PSModuleRuleProviderFactory : IRuleProviderFactory
    {
        private readonly string _absoluteModulePath;
        private readonly IAnalysisLogger? _logger;

        internal PSModuleRuleProviderFactory(string absoluteModulePath, IAnalysisLogger? logger)
        {
            _absoluteModulePath = absoluteModulePath;
            _logger = logger;
        }

        public IRuleProvider CreateRuleProvider(Builder.RuleComponentProvider ruleComponentProvider)
        {
            if (_absoluteModulePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                ManifestAuditResult auditResult = ModuleManifestAuditor.Audit(_absoluteModulePath, _logger);
                if (!auditResult.IsValid)
                {
                    _logger?.Warning($"Module manifest audit failed for '{_absoluteModulePath}': {auditResult.RejectionReason}");
                    return new EmptyRuleProvider();
                }
            }

            Runspace runspace = ConstrainedRuleRunspaceFactory.CreateConstrainedRunspace(_logger);

            try
            {
                ConstrainedRuleRunspaceFactory.ImportModuleAndLockDown(runspace, _absoluteModulePath, _logger);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to import module '{_absoluteModulePath}': {ex.Message}");
                runspace.Dispose();
                return new EmptyRuleProvider();
            }

            return new PSModuleRuleProvider(_absoluteModulePath, runspace, _logger);
        }
    }

    internal sealed class EmptyRuleProvider : IRuleProvider
    {
        public IEnumerable<RuleInfo> GetRuleInfos()
        {
            yield break;
        }

        public IEnumerable<ScriptRule> GetScriptRules()
        {
            yield break;
        }

        public void ReturnRule(Rule rule)
        {
        }
    }
}
