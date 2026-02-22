using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PSpecter.CommandDatabase.Import.LegacySettings;
using PSpecter.CommandDatabase.Sqlite;

namespace PSpecter.CommandDatabase.Import
{
    /// <summary>
    /// Imports command metadata from Engine/Settings JSON files (the legacy format
    /// used by UseCompatibleCmdlets). File names encode the platform:
    /// {edition}-{version}-{os}.json, e.g. "core-6.1.0-windows.json".
    /// </summary>
    public static class LegacySettingsImporter
    {
        /// <summary>
        /// Imports all JSON files from a settings directory into the database.
        /// </summary>
        public static void ImportDirectory(SqliteConnection connection, string settingsDirectory)
        {
            if (!Directory.Exists(settingsDirectory))
            {
                throw new DirectoryNotFoundException($"Settings directory not found: {settingsDirectory}");
            }

            using var writer = CommandDatabaseWriter.Begin(connection);

            foreach (string filePath in Directory.GetFiles(settingsDirectory, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!TryParsePlatformFromFileName(fileName, out string? edition, out string? version, out string? os))
                {
                    continue;
                }

                var platform = new PlatformInfo(edition!, version!, os!);
                string json = File.ReadAllText(filePath);
                var commands = ParseJson(json);
                writer.ImportCommands(commands, platform);
            }

            writer.Commit();
        }

        /// <summary>
        /// Imports a single legacy settings JSON string for a given platform.
        /// </summary>
        public static void ImportJson(CommandDatabaseWriter writer, string json, PlatformInfo platform)
        {
            var commands = ParseJson(json);
            writer.ImportCommands(commands, platform);
        }

        /// <summary>
        /// Parses a legacy settings JSON string into a list of <see cref="CommandMetadata"/>.
        /// </summary>
        internal static IReadOnlyList<CommandMetadata> ParseJson(string json)
        {
            var root = JsonConvert.DeserializeObject<SettingsRoot>(json);
            if (root?.Modules is null)
            {
                return Array.Empty<CommandMetadata>();
            }

            var result = new List<CommandMetadata>();

            foreach (Module module in root!.Modules)
            {
                string? moduleName = module.Name;

                if (module.ExportedCommands is not null)
                {
                    foreach (Command cmd in module.ExportedCommands)
                    {
                        if (string.IsNullOrWhiteSpace(cmd?.Name))
                        {
                            continue;
                        }

                        result.Add(new CommandMetadata(
                            name: cmd.Name,
                            commandType: cmd.CommandType ?? "Cmdlet",
                            moduleName: moduleName,
                            defaultParameterSet: null,
                            parameterSetNames: null,
                            aliases: null,
                            parameters: null,
                            outputTypes: null));
                    }
                }

                if (module.ExportedAliases is not null)
                {
                    foreach (string aliasName in module.ExportedAliases)
                    {
                        if (string.IsNullOrWhiteSpace(aliasName))
                        {
                            continue;
                        }

                        result.Add(new CommandMetadata(
                            name: aliasName,
                            commandType: "Alias",
                            moduleName: moduleName,
                            defaultParameterSet: null,
                            parameterSetNames: null,
                            aliases: null,
                            parameters: null,
                            outputTypes: null));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a filename like "core-6.1.0-windows" into platform components.
        /// </summary>
        internal static bool TryParsePlatformFromFileName(string fileName, out string? edition, out string? version, out string? os)
        {
            edition = null;
            version = null;
            os = null;

            int firstDash = fileName.IndexOf('-');
            if (firstDash < 0)
            {
                return false;
            }

            int lastDash = fileName.LastIndexOf('-');
            if (lastDash <= firstDash)
            {
                return false;
            }

            edition = fileName.Substring(0, firstDash);
            version = fileName.Substring(firstDash + 1, lastDash - firstDash - 1);
            os = fileName.Substring(lastDash + 1);

            if (string.Equals(edition, "core", StringComparison.OrdinalIgnoreCase))
            {
                edition = "Core";
            }
            else if (string.Equals(edition, "desktop", StringComparison.OrdinalIgnoreCase))
            {
                edition = "Desktop";
            }

            return !string.IsNullOrEmpty(edition) && !string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(os);
        }
    }
}
