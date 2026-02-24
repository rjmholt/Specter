using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Specter.Logging;

namespace Specter.Security
{
    /// <summary>
    /// Analyzer-scoped shared runspace pool used by PowerShell rule modules.
    /// Tracks loaded modules so multiple providers can reuse one pool.
    /// </summary>
    internal sealed class SharedRuleRunspacePool
    {
        private readonly ConcurrentDictionary<string, Lazy<string>> _modulePathToName;
        private readonly Lazy<RunspacePool> _runspacePool;
        private readonly IAnalysisLogger? _logger;

        internal SharedRuleRunspacePool(IAnalysisLogger? logger)
        {
            _logger = logger;
            _modulePathToName = new ConcurrentDictionary<string, Lazy<string>>(StringComparer.OrdinalIgnoreCase);
            _runspacePool = new Lazy<RunspacePool>(
                () => ConstrainedRuleRunspaceFactory.CreateConstrainedRunspacePool(_logger),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal RunspacePool RunspacePool => _runspacePool.Value;

        internal string EnsureModuleLoaded(string absoluteModulePath)
        {
            Lazy<string> moduleNameLazy = _modulePathToName.GetOrAdd(
                absoluteModulePath,
                path => new Lazy<string>(
                    () => ImportModule(path),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return moduleNameLazy.Value;
            }
            catch
            {
                _modulePathToName.TryRemove(absoluteModulePath, out _);
                throw;
            }
        }

        private string ImportModule(string absoluteModulePath)
        {
            using var ps = PowerShell.Create();
            ps.RunspacePool = RunspacePool;
            ps.AddCommand("Import-Module")
                .AddParameter("Name", absoluteModulePath)
                .AddParameter("Force")
                .AddParameter("DisableNameChecking")
                .AddParameter("PassThru");

            var modules = ps.Invoke();
            if (ps.HadErrors)
            {
                string firstError = ps.Streams.Error.Count > 0
                    ? ps.Streams.Error[0].ToString()
                    : "Unknown module import error";
                throw new InvalidOperationException(
                    $"Import-Module failed for '{absoluteModulePath}': {firstError}");
            }

            for (int i = 0; i < modules.Count; i++)
            {
                object baseObject = modules[i].BaseObject;
                if (baseObject is PSModuleInfo moduleInfo)
                {
                    _logger?.Debug(
                        $"Imported external rule module '{moduleInfo.Name}' from '{absoluteModulePath}' into shared pool.");
                    return moduleInfo.Name;
                }
            }

            throw new InvalidOperationException(
                $"Import-Module did not return module info for '{absoluteModulePath}'.");
        }
    }
}
