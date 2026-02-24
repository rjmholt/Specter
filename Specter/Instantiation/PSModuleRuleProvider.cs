using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using Specter.Logging;
using Specter.Rules;
using Specter.Security;

namespace Specter.Instantiation
{
    /// <summary>
    /// Provides rules discovered from a PowerShell module loaded in a constrained runspace pool.
    /// Runspace pool lifetime is shared and owned by the component provider.
    /// </summary>
    internal sealed class PSModuleRuleProvider : IRuleProvider, IDisposable
    {
        private readonly string _moduleName;
        private readonly RunspacePool _runspacePool;
        private readonly IAnalysisLogger? _logger;
        private readonly List<DiscoveredPSRule> _discoveredRules;
        private readonly ConcurrentDictionary<string, PSFunctionRule> _ruleInstances;

        internal PSModuleRuleProvider(
            string moduleName,
            RunspacePool runspacePool,
            IAnalysisLogger? logger)
        {
            _moduleName = moduleName;
            _runspacePool = runspacePool;
            _logger = logger;
            _discoveredRules = PSRuleDiscovery.DiscoverRules(runspacePool, moduleName, logger);
            _ruleInstances = new ConcurrentDictionary<string, PSFunctionRule>(StringComparer.OrdinalIgnoreCase);
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
                string qualifiedCommandName = $"{_moduleName}\\{discovered.FunctionName}";

                PSFunctionRule rule = _ruleInstances.GetOrAdd(key, _ => new PSFunctionRule(
                    discovered.RuleInfo,
                    _runspacePool,
                    qualifiedCommandName,
                    discovered.Convention,
                    _logger));

                yield return rule;
            }
        }

        public void ReturnRule(Rule rule)
        {
            // PSFunctionRules are reused (not pooled), nothing to return
        }

        public void Dispose()
        {
            _ruleInstances.Clear();
        }
    }

    /// <summary>
    /// Factory that creates a PSModuleRuleProvider by:
    /// 1. Auditing the module manifest (if .psd1)
    /// 2. Loading the module into the shared constrained runspace pool
    /// 3. Discovering module-specific rules
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

            if (!ruleComponentProvider.TryGetComponentInstance<SharedRuleRunspacePool>(out SharedRuleRunspacePool? sharedPool)
                || sharedPool is null)
            {
                _logger?.Error("SharedRuleRunspacePool component is not registered.");
                return new EmptyRuleProvider();
            }

            string moduleName;
            try
            {
                moduleName = sharedPool.EnsureModuleLoaded(_absoluteModulePath);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to load module '{_absoluteModulePath}' into shared runspace pool: {ex.Message}");
                return new EmptyRuleProvider();
            }

            return new PSModuleRuleProvider(moduleName, sharedPool.RunspacePool, _logger);
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
