using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter;
using PSpecter.Builtin.Rules;
using PSpecter.CommandDatabase;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    public record UseCompatibleCommandsConfiguration : IRuleConfiguration
    {
        public string[] TargetProfiles { get; init; } = Array.Empty<string>();
        public string[] IgnoreCommands { get; init; } = Array.Empty<string>();
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enabled: false);
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCompatibleCommands", typeof(Strings), nameof(Strings.UseCompatibleCommandsDescription))]
    public class UseCompatibleCommands : ConfigurableScriptRule<UseCompatibleCommandsConfiguration>
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        public UseCompatibleCommands(
            RuleInfo ruleInfo,
            UseCompatibleCommandsConfiguration configuration,
            IPowerShellCommandDatabase commandDb)
            : base(ruleInfo, configuration)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string[] targetProfiles = Configuration.TargetProfiles;
            if (targetProfiles is null || targetProfiles.Length == 0)
            {
                yield break;
            }

            var ignoreSet = BuildIgnoreSet(Configuration.IgnoreCommands);
            var resolvedTargets = ResolveTargetProfiles(targetProfiles);
            if (resolvedTargets.Count == 0)
            {
                yield break;
            }

            var unionPlatforms = BuildUnionPlatforms(resolvedTargets);

            foreach (Ast foundAst in ast.FindAll(node => node is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string? commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                if (ignoreSet is not null && ignoreSet.Contains(commandName!))
                {
                    continue;
                }

                string resolvedName = ResolveAlias(commandName!);

                if (!_commandDb.CommandExistsOnPlatform(resolvedName, unionPlatforms))
                {
                    continue;
                }

                foreach (var target in resolvedTargets)
                {
                    var targetPlatforms = new HashSet<PlatformInfo> { target.Platform };

                    if (!_commandDb.CommandExistsOnPlatform(resolvedName, targetPlatforms))
                    {
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseCompatibleCommandsCommandError,
                            commandName,
                            target.Platform.Version,
                            target.Platform.Os.Family);

                        var diag = CreateDiagnostic(message, cmdAst);
                        diag.Properties = CreateCommandProperties(commandName!, null, target.Platform);
                        yield return diag;
                        continue;
                    }

                    if (_commandDb.TryGetCommand(resolvedName, targetPlatforms, out CommandMetadata? targetCmd)
                        && targetCmd is not null)
                    {
                        var targetParamSet = BuildParameterNameSet(targetCmd);

                        foreach (var paramDiag in CheckParameters(cmdAst, commandName!, target, targetParamSet))
                        {
                            yield return paramDiag;
                        }
                    }
                }
            }
        }

        private string ResolveAlias(string nameOrAlias)
        {
            string? target = _commandDb.GetAliasTarget(nameOrAlias);
            return target ?? nameOrAlias;
        }

        private IEnumerable<ScriptDiagnostic> CheckParameters(
            CommandAst cmdAst,
            string commandName,
            ResolvedTarget target,
            HashSet<string> targetParams)
        {
            for (int i = 0; i < cmdAst.CommandElements.Count; i++)
            {
                if (cmdAst.CommandElements[i] is not CommandParameterAst paramAst)
                {
                    continue;
                }

                if (targetParams.Contains(paramAst.ParameterName))
                {
                    continue;
                }

                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UseCompatibleCommandsParameterError,
                    paramAst.ParameterName,
                    commandName,
                    target.Platform.Version,
                    target.Platform.Os.Family);

                var diag = CreateDiagnostic(message, paramAst);
                diag.Properties = CreateCommandProperties(commandName, paramAst.ParameterName, target.Platform);
                yield return diag;
            }
        }

        private List<ResolvedTarget> ResolveTargetProfiles(string[] profileNames)
        {
            var targets = new List<ResolvedTarget>(profileNames.Length);

            foreach (string profileName in profileNames)
            {
                if (_commandDb.TryResolveProfile(profileName, out PlatformInfo? platform) && platform is not null)
                {
                    targets.Add(new ResolvedTarget(profileName, platform));
                }
            }

            return targets;
        }

        private static HashSet<PlatformInfo> BuildUnionPlatforms(List<ResolvedTarget> targets)
        {
            var union = new HashSet<PlatformInfo>();
            foreach (var target in targets)
            {
                union.Add(target.Platform);
            }
            return union;
        }

        private static HashSet<string>? BuildIgnoreSet(string[]? ignoreCommands)
        {
            if (ignoreCommands is null || ignoreCommands.Length == 0)
            {
                return null;
            }

            return new HashSet<string>(ignoreCommands, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> BuildParameterNameSet(CommandMetadata cmd)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ParameterMetadata param in cmd.Parameters)
            {
                names.Add(param.Name);
            }
            return names;
        }

        private static Dictionary<string, object> CreateCommandProperties(
            string command, string? parameter, PlatformInfo platform)
        {
            var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Command"] = command,
                ["TargetPlatform"] = platform,
            };

            if (parameter is not null)
            {
                props["Parameter"] = parameter;
            }

            return props;
        }

        private readonly struct ResolvedTarget
        {
            public readonly string ProfileName;
            public readonly PlatformInfo Platform;

            public ResolvedTarget(string profileName, PlatformInfo platform)
            {
                ProfileName = profileName;
                Platform = platform;
            }
        }
    }
}
