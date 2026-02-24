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
    public record UseCompatibleCommandsConfiguration : IRuleConfiguration
    {
        public string[] TargetProfiles { get; init; } = Array.Empty<string>();
        public string[] IgnoreCommands { get; init; } = Array.Empty<string>();
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enable: false);
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCompatibleCommands", typeof(Strings), nameof(Strings.UseCompatibleCommandsDescription))]
    internal class UseCompatibleCommands : ConfigurableScriptRule<UseCompatibleCommandsConfiguration>
    {
        // Common parameters present since PS 2.0 that profiles don't enumerate.
        // Excludes PipelineVariable (PS 4.0), InformationAction/InformationVariable (PS 5.0)
        // which must be checked for version compatibility.
        private static readonly HashSet<string> s_universalCommonParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Debug", "ErrorAction", "ErrorVariable",
            "OutVariable", "OutBuffer", "Verbose",
            "WarningAction", "WarningVariable",
            "WhatIf", "Confirm",
        };

        private readonly IPowerShellCommandDatabase _commandDb;
        private readonly PlatformContext _platformContext;

        internal UseCompatibleCommands(
            RuleInfo ruleInfo,
            UseCompatibleCommandsConfiguration configuration,
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

            string[] targetProfiles = Configuration.TargetProfiles;
            List<ResolvedTarget> resolvedTargets;
            if (targetProfiles is null || targetProfiles.Length == 0)
            {
                resolvedTargets = ResolveTargetPlatforms(_platformContext.TargetPlatforms);
            }
            else
            {
                resolvedTargets = ResolveTargetProfiles(targetProfiles);
            }

            var ignoreSet = BuildIgnoreSet(Configuration.IgnoreCommands);
            if (resolvedTargets.Count == 0)
            {
                yield break;
            }

            var commandAsts = new List<CommandAst>();
            foreach (Ast foundAst in ast.FindAll(static node => node is CommandAst, searchNestedScriptBlocks: true))
            {
                commandAsts.Add((CommandAst)foundAst);
            }
            commandAsts.Sort(static (left, right) =>
                GetCommandTokenStartOffset(left).CompareTo(GetCommandTokenStartOffset(right)));

            foreach (CommandAst cmdAst in commandAsts)
            {
                string? commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                if (ignoreSet is not null && ignoreSet.Contains(commandName!))
                {
                    continue;
                }

                // Skip commands not known to any platform in the DB (user-defined functions).
                // Check both the raw name and alias-resolved name so we catch aliases that
                // exist on some platforms but not others.
                if (!_commandDb.CommandExistsOnPlatform(commandName!, platforms: null))
                {
                    string resolvedGlobal = ResolveAlias(commandName!);
                    if (string.Equals(resolvedGlobal, commandName, StringComparison.OrdinalIgnoreCase)
                        || !_commandDb.CommandExistsOnPlatform(resolvedGlobal, platforms: null))
                    {
                        continue;
                    }
                }

                foreach (var target in resolvedTargets)
                {
                    var targetPlatforms = new HashSet<PlatformInfo> { target.Platform };
                    string resolvedName = ResolveAlias(commandName!);
                    bool hasRawCommand = _commandDb.CommandExistsOnPlatform(commandName!, targetPlatforms);
                    bool hasResolvedCommand = !string.Equals(resolvedName, commandName, StringComparison.OrdinalIgnoreCase)
                        && _commandDb.CommandExistsOnPlatform(resolvedName, targetPlatforms);

                    if (!hasRawCommand && !hasResolvedCommand)
                    {
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseCompatibleCommandsCommandError,
                            commandName,
                            target.Platform.Version,
                            target.Platform.Os.Family);

                        yield return CreateDiagnostic(
                            message,
                            cmdAst,
                            RuleInfo.DefaultSeverity,
                            corrections: null,
                            ruleSuppressionId: null,
                            command: commandName!,
                            parameter: null,
                            targetPlatform: target.Platform);
                        continue;
                    }

                    string commandNameForMetadata = hasRawCommand ? commandName! : resolvedName;
                    if (_commandDb.TryGetCommand(commandNameForMetadata, targetPlatforms, out CommandMetadata? targetCmd)
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

                if (s_universalCommonParameters.Contains(paramAst.ParameterName))
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

                yield return CreateDiagnostic(
                    message,
                    paramAst,
                    RuleInfo.DefaultSeverity,
                    corrections: null,
                    ruleSuppressionId: null,
                    command: commandName,
                    parameter: paramAst.ParameterName,
                    targetPlatform: target.Platform);
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

        private static List<ResolvedTarget> ResolveTargetPlatforms(IReadOnlyList<PlatformInfo> platforms)
        {
            var targets = new List<ResolvedTarget>(platforms.Count);
            for (int i = 0; i < platforms.Count; i++)
            {
                PlatformInfo platform = platforms[i];
                targets.Add(new ResolvedTarget(platform.ToString(), platform));
            }

            return targets;
        }

        private static HashSet<string>? BuildIgnoreSet(string[]? ignoreCommands)
        {
            if (ignoreCommands is null || ignoreCommands.Length == 0)
            {
                return null;
            }

            return new HashSet<string>(ignoreCommands, StringComparer.OrdinalIgnoreCase);
        }

        private static int GetCommandTokenStartOffset(CommandAst commandAst)
        {
            if (commandAst.CommandElements.Count > 0)
            {
                return commandAst.CommandElements[0].Extent.StartOffset;
            }

            return commandAst.Extent.StartOffset;
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

        private readonly struct ResolvedTarget
        {
            public readonly string ProfileName;
            public readonly PlatformInfo Platform;

            internal ResolvedTarget(string profileName, PlatformInfo platform)
            {
                ProfileName = profileName;
                Platform = platform;
            }
        }

    }
}
