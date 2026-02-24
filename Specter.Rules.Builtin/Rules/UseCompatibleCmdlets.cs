using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter;
using Specter.Rules.Builtin.Rules;
using Specter.CommandDatabase;
using Specter.Configuration;
using Specter.Rules;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    public record UseCompatibleCmdletsConfiguration : IRuleConfiguration
    {
        public string[] Compatibility { get; init; } = Array.Empty<string>();
        public string? Reference { get; init; }
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enable: false);
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCompatibleCmdlets", typeof(Strings), nameof(Strings.UseCompatibleCmdletsDescription))]
    internal class UseCompatibleCmdlets : ConfigurableScriptRule<UseCompatibleCmdletsConfiguration>
    {
        private const string DefaultReference = "desktop-5.1.14393.206-windows";
        private const string AlternativeDefaultReference = "core-6.1.0-windows";

        private readonly IPowerShellCommandDatabase _commandDb;
        private readonly PlatformContext _platformContext;

        internal UseCompatibleCmdlets(
            RuleInfo ruleInfo,
            UseCompatibleCmdletsConfiguration configuration,
            IPowerShellCommandDatabase commandDb,
            PlatformContext platformContext)
            : base(ruleInfo, configuration)
        {
            _commandDb = commandDb;
            _platformContext = platformContext;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string[] compatibility = Configuration.Compatibility;
            bool useGlobalTargets = compatibility is null || compatibility.Length == 0;
            if (useGlobalTargets && _platformContext.TargetPlatforms.Count == 0)
            {
                yield break;
            }

            var targetPlatforms = new List<(string Label, PlatformInfo Platform)>();
            if (useGlobalTargets)
            {
                for (int i = 0; i < _platformContext.TargetPlatforms.Count; i++)
                {
                    PlatformInfo platform = _platformContext.TargetPlatforms[i];
                    targetPlatforms.Add((platform.ToString(), platform));
                }
            }
            else
            {
                string[] configuredCompatibility = compatibility ?? Array.Empty<string>();
                foreach (string platformStr in configuredCompatibility)
                {
                    if (PlatformInfo.TryParseFromLegacyProfileName(platformStr, out PlatformInfo? platform)
                        && platform is not null)
                    {
                        targetPlatforms.Add((platformStr, platform));
                    }
                }
            }

            if (targetPlatforms.Count == 0)
            {
                yield break;
            }

            string? referenceStr = Configuration.Reference;
            if (string.IsNullOrEmpty(referenceStr))
            {
                referenceStr = DefaultReference;
                if (!useGlobalTargets
                    && compatibility is not null
                    && targetPlatforms.Count == 1
                    && string.Equals(compatibility[0], DefaultReference, StringComparison.OrdinalIgnoreCase))
                {
                    referenceStr = AlternativeDefaultReference;
                }
            }

            if (!PlatformInfo.TryParseFromLegacyProfileName(referenceStr!, out PlatformInfo? refPlatform)
                || refPlatform is null)
            {
                yield break;
            }

            var referencePlatforms = new HashSet<PlatformInfo> { refPlatform };

            foreach (Ast foundAst in ast.FindAll(static node => node is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                bool existsOnReference = _commandDb.CommandExistsOnPlatform(commandName, referencePlatforms);

                foreach ((string label, PlatformInfo platform) in targetPlatforms)
                {
                    var platforms = new HashSet<PlatformInfo> { platform };
                    bool existsOnTarget = _commandDb.CommandExistsOnPlatform(commandName, platforms);

                    if (existsOnReference && !existsOnTarget)
                    {
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseCompatibleCmdletsError,
                            commandName,
                            platform.Edition,
                            platform.Version,
                            platform.Os.Family);

                        yield return CreateDiagnostic(message, cmdAst);
                    }
                }
            }
        }
    }
}
