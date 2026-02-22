using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace PSpecter.Runtime
{
    public interface IPowerShellCommandDatabase
    {
        /// <summary>
        /// Looks up a command by canonical name or alias.
        /// Returns a rich <see cref="CommandMetadata"/> if found.
        /// Implementations that don't support rich metadata may return false.
        /// </summary>
        bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo> platforms, out CommandMetadata command);

        IReadOnlyList<string> GetCommandAliases(string command);

        string GetAliasTarget(string alias);

        IReadOnlyList<string> GetAllNamesForCommand(string command);
    }

    public static class CommandDatabaseExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="name"/> is either the canonical command
        /// name or one of its known aliases.
        /// </summary>
        public static bool IsCommandOrAlias(this IPowerShellCommandDatabase db, string name, string canonicalCommand)
        {
            if (string.Equals(name, canonicalCommand, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string target = db.GetAliasTarget(name);
            return string.Equals(target, canonicalCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class SessionStateCommandDatabase : IPowerShellCommandDatabase
    {
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

            return new SessionStateCommandDatabase(commandAliases, aliasTargets);
        }

        private readonly IReadOnlyDictionary<string, string> _aliasTargets;

        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _commandAliases;

        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _commandNames;

        private SessionStateCommandDatabase(
            IReadOnlyDictionary<string, IReadOnlyList<string>> commandAliases,
            IReadOnlyDictionary<string, string> aliasTargets)
        {
            _aliasTargets = aliasTargets;
            _commandAliases = commandAliases;
            _commandNames = new ConcurrentDictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Not supported by the session-state-only database; always returns false.
        /// </summary>
        public bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo> platforms, out CommandMetadata command)
        {
            command = null;
            return false;
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
