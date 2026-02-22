using System;
using System.Collections.Generic;

namespace PSpecter.CommandDatabase
{
    public interface IPowerShellCommandDatabase
    {
        /// <summary>
        /// Looks up a command by canonical name or alias.
        /// Returns a rich <see cref="CommandMetadata"/> if found.
        /// Implementations that don't support rich metadata may return false.
        /// </summary>
        bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo>? platforms, out CommandMetadata? command);

        IReadOnlyList<string>? GetCommandAliases(string command);

        string? GetAliasTarget(string alias);

        IReadOnlyList<string>? GetAllNamesForCommand(string command);

        /// <summary>
        /// Returns true if the given command (or alias) exists on any of the specified platforms.
        /// When <paramref name="platforms"/> is null, the check is platform-agnostic.
        /// </summary>
        bool CommandExistsOnPlatform(string nameOrAlias, HashSet<PlatformInfo>? platforms);
    }

    public static class CommandDatabaseExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="name"/> is either the canonical command
        /// name or one of its known aliases.
        /// </summary>
        public static bool IsCommandOrAlias(this IPowerShellCommandDatabase db, string? name, string canonicalCommand)
        {
            if (name is null)
            {
                return false;
            }

            if (string.Equals(name, canonicalCommand, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string? target = db.GetAliasTarget(name);
            return string.Equals(target, canonicalCommand, StringComparison.OrdinalIgnoreCase);
        }
    }
}
