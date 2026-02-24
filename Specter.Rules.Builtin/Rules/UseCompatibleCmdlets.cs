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
        private const string DefaultWindowsReference = "desktop-5.1.14393.206-windows";

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
                foreach (PlatformInfo platform in ResolveBuiltinTargetPlatforms())
                {
                    targetPlatforms.Add((platform.ToString(), platform));
                }
            }
            else
            {
                string[] configuredCompatibility = compatibility ?? Array.Empty<string>();
                foreach (string platformStr in configuredCompatibility)
                {
                    if (_commandDb.TryResolveProfile(platformStr, out PlatformInfo? resolvedPlatform)
                        && resolvedPlatform is not null)
                    {
                        targetPlatforms.Add((platformStr, resolvedPlatform));
                        continue;
                    }

                    if (PlatformInfo.TryParseFromLegacyProfileName(platformStr, out PlatformInfo? parsedPlatform)
                        && parsedPlatform is not null)
                    {
                        targetPlatforms.Add((platformStr, parsedPlatform));
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
                referenceStr = DefaultWindowsReference;
            }

            PlatformInfo? refPlatform = null;
            if (!_commandDb.TryResolveProfile(referenceStr!, out refPlatform) || refPlatform is null)
            {
                if (!PlatformInfo.TryParseFromLegacyProfileName(referenceStr!, out refPlatform) || refPlatform is null)
                {
                    yield break;
                }
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

        private IEnumerable<PlatformInfo> ResolveBuiltinTargetPlatforms()
        {
            var defaults = new List<PlatformInfo>();

            TryAddResolvedPlatform(DefaultWindowsReference, defaults);

            string currentPs7Profile = $"core-7.0.0-{GetCurrentOsToken()}";
            TryAddResolvedPlatform(currentPs7Profile, defaults);

            if (defaults.Count > 0)
            {
                return defaults;
            }

            return _platformContext.TargetPlatforms;
        }

        private void TryAddResolvedPlatform(string profileName, List<PlatformInfo> platforms)
        {
            if (_commandDb.TryResolveProfile(profileName, out PlatformInfo? resolved) && resolved is not null)
            {
                AddUnique(platforms, resolved);
                return;
            }

            if (PlatformInfo.TryParseFromLegacyProfileName(profileName, out PlatformInfo? parsed) && parsed is not null)
            {
                AddUnique(platforms, parsed);
            }
        }

        private static void AddUnique(List<PlatformInfo> platforms, PlatformInfo platform)
        {
            for (int i = 0; i < platforms.Count; i++)
            {
                if (platforms[i].Equals(platform))
                {
                    return;
                }
            }

            platforms.Add(platform);
        }

        private static string GetCurrentOsToken()
        {
#if CORECLR
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return "windows";
            }

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return "macos";
            }

            return "linux";
#else
            return "windows";
#endif
        }
    }
}
