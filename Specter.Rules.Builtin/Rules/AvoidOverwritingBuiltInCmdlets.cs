using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Configuration;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
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
        private readonly PlatformContext _platformContext;

        internal AvoidOverwritingBuiltInCmdlets(
            RuleInfo ruleInfo,
            AvoidOverwritingBuiltInCmdletsConfiguration configuration,
            IPowerShellCommandDatabase commandDatabase,
            PlatformContext platformContext)
            : base(ruleInfo, configuration)
        {
            _commandDatabase = commandDatabase;
            _platformContext = platformContext;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            List<PlatformInfo>? targetPlatforms = ResolveTargetPlatforms();

            foreach (Ast node in ast.FindAll(static testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;
                string functionName = funcAst.Name;

                if (targetPlatforms is null || targetPlatforms.Count == 0)
                {
                    if (!IsBuiltinCommand(functionName))
                    {
                        continue;
                    }

                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.AvoidOverwritingBuiltInCmdletsError,
                            functionName,
                            "PowerShell built-in cmdlet baseline"),
                        funcAst.Extent);
                    continue;
                }

                for (int i = 0; i < targetPlatforms.Count; i++)
                {
                    PlatformInfo platform = targetPlatforms[i];
                    if (!CommandExistsOnTarget(functionName, platform))
                    {
                        continue;
                    }

                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.AvoidOverwritingBuiltInCmdletsError,
                            functionName,
                            platform.ToString()),
                        funcAst.Extent);
                }
            }
        }

        private bool IsBuiltinCommand(string commandName)
        {
            if (!_commandDatabase.TryGetCommand(commandName, platforms: null, out CommandMetadata? command)
                || command is null)
            {
                return false;
            }

            return command.IsBuiltin && string.Equals(command.CommandType, "Cmdlet", StringComparison.OrdinalIgnoreCase);
        }

        private bool CommandExistsOnTarget(string commandName, PlatformInfo platform)
        {
            var target = new HashSet<PlatformInfo> { platform };
            return _commandDatabase.CommandExistsOnPlatform(commandName, target);
        }

        private List<PlatformInfo>? ResolveTargetPlatforms()
        {
            if (Configuration.PowerShellVersion is { Length: > 0 })
            {
                var resolved = new List<PlatformInfo>(Configuration.PowerShellVersion.Length);
                for (int i = 0; i < Configuration.PowerShellVersion.Length; i++)
                {
                    string profile = Configuration.PowerShellVersion[i];
                    if (string.IsNullOrWhiteSpace(profile))
                    {
                        continue;
                    }

                    if (_commandDatabase.TryResolveProfile(profile, out PlatformInfo? resolvedPlatform)
                        && resolvedPlatform is not null)
                    {
                        resolved.Add(resolvedPlatform);
                    }
                }

                return resolved.Count > 0 ? resolved : null;
            }

            if (_platformContext.TargetPlatforms.Count > 0)
            {
                var targets = new List<PlatformInfo>(_platformContext.TargetPlatforms.Count);
                for (int i = 0; i < _platformContext.TargetPlatforms.Count; i++)
                {
                    targets.Add(_platformContext.TargetPlatforms[i]);
                }

                return targets;
            }

            return null;
        }
    }
}
