using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using Specter.CommandDatabase;
using Specter.Configuration;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    internal class AvoidOverwritingBuiltInCmdletsConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enable: true);

        public string[] PowerShellVersion { get; set; } = Array.Empty<string>();
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidOverwritingBuiltInCmdlets", typeof(Strings), nameof(Strings.AvoidOverwritingBuiltInCmdletsDescription))]
    internal class AvoidOverwritingBuiltInCmdlets : ConfigurableScriptRule<AvoidOverwritingBuiltInCmdletsConfiguration>
    {
        private static readonly object s_lookupLock = new object();
        private static HashSet<string>? s_defaultCmdletLookup;

        private readonly IPowerShellCommandDatabase _commandDatabase;

        internal AvoidOverwritingBuiltInCmdlets(
            RuleInfo ruleInfo,
            AvoidOverwritingBuiltInCmdletsConfiguration configuration,
            IPowerShellCommandDatabase commandDatabase)
            : base(ruleInfo, configuration)
        {
            _commandDatabase = commandDatabase;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            HashSet<PlatformInfo>? targetPlatforms = ResolveTargetPlatforms(Configuration.PowerShellVersion);
            string targetLabel = GetTargetLabel(targetPlatforms);

            foreach (Ast node in ast.FindAll(static testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;
                string functionName = funcAst.Name;

                if (IsBuiltinCommand(functionName, targetPlatforms))
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.AvoidOverwritingBuiltInCmdletsError,
                            functionName,
                            targetLabel),
                        funcAst.Extent);
                }
            }
        }

        private bool IsBuiltinCommand(string commandName, HashSet<PlatformInfo>? targetPlatforms)
        {
            if (_commandDatabase.CommandExistsOnPlatform(commandName, targetPlatforms))
            {
                return true;
            }

            return targetPlatforms == null && GetDefaultCmdletLookup().Contains(commandName);
        }

        private static HashSet<PlatformInfo>? ResolveTargetPlatforms(string[] configuredProfiles)
        {
            if (configuredProfiles == null || configuredProfiles.Length == 0)
            {
                return null;
            }

            var platforms = new HashSet<PlatformInfo>();
            for (int i = 0; i < configuredProfiles.Length; i++)
            {
                string profile = configuredProfiles[i];
                if (string.IsNullOrWhiteSpace(profile))
                {
                    continue;
                }

                if (PlatformInfo.TryParseFromLegacyProfileName(profile, out PlatformInfo? platform)
                    && platform is not null)
                {
                    platforms.Add(platform);
                }
            }

            return platforms.Count == 0 ? null : platforms;
        }

        private static string GetTargetLabel(HashSet<PlatformInfo>? platforms)
        {
            if (platforms == null || platforms.Count == 0)
            {
                return "current target platforms";
            }

            foreach (PlatformInfo platform in platforms)
            {
                return platform.ToString();
            }

            return "configured target platforms";
        }

        private static HashSet<string> GetDefaultCmdletLookup()
        {
            if (s_defaultCmdletLookup is not null)
            {
                return s_defaultCmdletLookup;
            }

            lock (s_lookupLock)
            {
                if (s_defaultCmdletLookup is null)
                {
                    s_defaultCmdletLookup = BuildDefaultCmdletNameSet();
                }
            }

            return s_defaultCmdletLookup;
        }

        private static HashSet<string> BuildDefaultCmdletNameSet()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                InitialSessionState iss = InitialSessionState.CreateDefault();
                foreach (SessionStateCommandEntry entry in iss.Commands)
                {
                    if (entry.CommandType == CommandTypes.Cmdlet
                        && !string.IsNullOrEmpty(entry.Name))
                    {
                        names.Add(entry.Name);
                    }
                }
            }
            catch
            {
                // Keep empty fallback if discovery fails; command DB check still runs.
            }

            return names;
        }
    }
}
