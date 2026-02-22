using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSpecter.CommandDatabase
{
    public class SessionStateCommandDatabase : IPowerShellCommandDatabase
    {
        private const CommandTypes ResolvableCommandTypes =
            CommandTypes.Cmdlet | CommandTypes.Function | CommandTypes.ExternalScript;

        public static SessionStateCommandDatabase Create(CommandInvocationIntrinsics invokeCommandProvider)
        {
            var commandAliases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var aliasTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (AliasInfo aliasInfo in invokeCommandProvider.GetCommands("*", CommandTypes.Alias, nameIsPattern: true))
            {
                aliasTargets[aliasInfo.Name] = aliasInfo.Definition;

                if (commandAliases.TryGetValue(aliasInfo.Definition, out IReadOnlyList<string> aliases))
                {
                    ((List<string>)aliases).Add(aliasInfo.Name);
                }
                else
                {
                    commandAliases[aliasInfo.Definition] = new List<string> { aliasInfo.Name };
                }
            }

            return new SessionStateCommandDatabase(invokeCommandProvider, commandAliases, aliasTargets);
        }

        private readonly CommandInvocationIntrinsics _invokeCommand;

        private readonly IReadOnlyDictionary<string, string> _aliasTargets;

        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _commandAliases;

        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _commandNames;

        /// <summary>
        /// Lazily caches resolved commands so we only call GetCommand() once per name.
        /// A null value means we checked and the command doesn't exist.
        /// </summary>
        private readonly ConcurrentDictionary<string, CommandMetadata> _commandCache;

        private SessionStateCommandDatabase(
            CommandInvocationIntrinsics invokeCommand,
            IReadOnlyDictionary<string, IReadOnlyList<string>> commandAliases,
            IReadOnlyDictionary<string, string> aliasTargets)
        {
            _invokeCommand = invokeCommand;
            _aliasTargets = aliasTargets;
            _commandAliases = commandAliases;
            _commandNames = new ConcurrentDictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            _commandCache = new ConcurrentDictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo> platforms, out CommandMetadata command)
        {
            string resolved = GetAliasTarget(nameOrAlias) ?? nameOrAlias;

            // Use a sentinel check: the cache stores null for commands that don't exist
            if (_commandCache.TryGetValue(resolved, out command))
            {
                return command is not null;
            }

            command = ResolveCommand(resolved);
            _commandCache[resolved] = command;
            return command is not null;
        }

        private CommandMetadata ResolveCommand(string commandName)
        {
            CommandInfo cmdInfo;
            try
            {
                cmdInfo = _invokeCommand.GetCommand(commandName, ResolvableCommandTypes);
            }
            catch
            {
                return null;
            }

            if (cmdInfo is null)
            {
                return null;
            }

            var parameters = new List<ParameterMetadata>();
            try
            {
                foreach (var kvp in cmdInfo.Parameters)
                {
                    parameters.Add(new ParameterMetadata(
                        kvp.Key,
                        kvp.Value.ParameterType?.FullName,
                        isDynamic: kvp.Value.IsDynamic,
                        parameterSets: System.Array.Empty<ParameterSetInfo>()));
                }
            }
            catch
            {
                // Some commands may throw when accessing Parameters
            }

            return new CommandMetadata(
                cmdInfo.Name,
                cmdInfo.CommandType.ToString(),
                cmdInfo.ModuleName ?? string.Empty,
                defaultParameterSet: null,
                parameterSetNames: System.Array.Empty<string>(),
                aliases: System.Array.Empty<string>(),
                parameters: parameters,
                outputTypes: System.Array.Empty<string>());
        }

        public string GetAliasTarget(string alias)
        {
            if (_aliasTargets.TryGetValue(alias, out string target))
            {
                return target;
            }

            return null;
        }

        public IReadOnlyList<string> GetCommandAliases(string command)
        {
            if (_commandAliases.TryGetValue(command, out IReadOnlyList<string> aliases))
            {
                return aliases;
            }

            return null;
        }

        public IReadOnlyList<string> GetAllNamesForCommand(string command)
        {
            return _commandNames.GetOrAdd(command, GenerateCommandNameList);
        }

        private IReadOnlyList<string> GenerateCommandNameList(string command)
        {
            var names = new List<string>();

            if (_commandAliases.TryGetValue(command, out IReadOnlyList<string> aliases))
            {
                names.AddRange(aliases);
            }

            if (_aliasTargets.TryGetValue(command, out string target))
            {
                names.Add(target);
            }

            return names.Count > 0 ? names : null;
        }
    }
}
