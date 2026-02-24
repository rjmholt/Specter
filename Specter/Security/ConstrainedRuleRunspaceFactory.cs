using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Specter.Logging;

namespace Specter.Security
{
    /// <summary>
    /// Creates runspaces for external PowerShell rule modules with restricted command visibility.
    /// Uses FullLanguage mode so rule authors have access to the full PowerShell language surface
    /// (object creation, .NET method calls, hashtable literals, etc.) needed for effective analysis.
    ///
    /// ConstrainedLanguage mode was deliberately rejected because:
    ///   1. CLM only provides a meaningful security boundary when enforced system-wide via
    ///      WDAC/AppLocker. Without those, it is trivially bypassable in a custom runspace.
    ///   2. CLM prevents rule authors from doing essential work like calling AST methods,
    ///      constructing diagnostic objects, and using [hashtable]::new().
    ///
    /// The real security boundary is the opt-in gate: explicit user configuration, path validation,
    /// file ownership checks, and manifest auditing. Command visibility restrictions are kept as
    /// defense-in-depth against accidental side effects (no file writes, no network, no processes).
    /// </summary>
    internal static class ConstrainedRuleRunspaceFactory
    {
        private static readonly HashSet<string> s_allowedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Inspection
            "Get-Command",
            "Get-Module",
            "Get-Member",
            "Get-Help",

            // Pipeline / collection
            "Where-Object",
            "ForEach-Object",
            "Select-Object",
            "Sort-Object",
            "Group-Object",
            "Measure-Object",
            "Compare-Object",

            // Output
            "Write-Output",
            "Write-Warning",
            "Write-Verbose",
            "Write-Debug",
            "Write-Error",
            "Out-Null",
            "Out-String",

            // Path manipulation (read-only)
            "Test-Path",
            "Split-Path",
            "Join-Path",
            "Resolve-Path",

            // Object construction
            "New-Object",

            // Data / localization (read-only, used by CommunityAnalyzerRules and similar)
            "Import-LocalizedData",
            "ConvertFrom-StringData",

            // Setup/runtime module loading (required for shared pool loading)
            "Import-Module",
        };

        internal static RunspacePool CreateConstrainedRunspacePool(IAnalysisLogger? logger)
        {
            var iss = InitialSessionState.CreateDefault();

            RestrictCommands(iss);
            RemoveDangerousProviders(iss);
            ClearPSModulePath(iss);

            int maxRunspaces = Math.Max(1, Environment.ProcessorCount);
            var runspacePool = RunspaceFactory.CreateRunspacePool(1, maxRunspaces, iss, host: null);
            runspacePool.Open();

            logger?.Debug(
                $"Created rule runspace pool with restricted command visibility (size 1..{maxRunspaces}).");
            return runspacePool;
        }

        private static void RestrictCommands(InitialSessionState iss)
        {
            foreach (SessionStateCommandEntry entry in iss.Commands)
            {
                if (entry.CommandType == CommandTypes.Cmdlet || entry.CommandType == CommandTypes.Function)
                {
                    entry.Visibility = s_allowedCommands.Contains(entry.Name)
                        ? SessionStateEntryVisibility.Public
                        : SessionStateEntryVisibility.Private;
                }
            }
        }

        private static void RemoveDangerousProviders(InitialSessionState iss)
        {
            var toRemove = new List<string>();
            foreach (SessionStateProviderEntry provider in iss.Providers)
            {
                string name = provider.Name;
                // Keep Function, Variable, and Alias for normal PS operation.
                // Remove FileSystem, Registry, Environment, Certificate to prevent side effects.
                if (name.Equals("FileSystem", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Registry", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Environment", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
                {
                    toRemove.Add(name);
                }
            }

            foreach (string name in toRemove)
            {
                iss.Providers.Remove(name, null);
            }
        }

        private static void ClearPSModulePath(InitialSessionState iss)
        {
#if CORECLR
            iss.EnvironmentVariables.Add(
                new SessionStateVariableEntry("PSModulePath", string.Empty, "Cleared by Specter security policy"));
#else
            iss.Variables.Add(
                new SessionStateVariableEntry("env:PSModulePath", string.Empty, "Cleared by Specter security policy"));
#endif
        }

    }
}
