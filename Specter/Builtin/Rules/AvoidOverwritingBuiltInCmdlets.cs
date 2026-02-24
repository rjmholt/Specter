using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
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
            return _commandDatabase.CommandExistsOnPlatform(commandName, targetPlatforms);
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

    }
}
