using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Specter.CommandDatabase.Import.LegacySettings;
using Specter.CommandDatabase.Sqlite;

namespace Specter.CommandDatabase.Import
{
    /// <summary>
    /// Imports command metadata from Engine/Settings JSON files (the legacy format
    /// used by UseCompatibleCmdlets). File names encode the platform:
    /// {edition}-{version}-{os}.json, e.g. "core-6.1.0-windows.json".
    /// </summary>
    internal static class LegacySettingsImporter
    {
        /// <summary>
        /// Imports all JSON files from a settings directory into the database.
        /// </summary>
        internal static void ImportDirectory(SqliteConnection connection, string settingsDirectory)
        {
            if (!Directory.Exists(settingsDirectory))
            {
                throw new DirectoryNotFoundException($"Settings directory not found: {settingsDirectory}");
            }

            using var writer = CommandDatabaseWriter.Begin(connection);

            foreach (string filePath in Directory.GetFiles(settingsDirectory, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!TryParsePlatformFromFileName(fileName, out PlatformInfo? platform) || platform is null)
                {
                    continue;
                }

                string json = File.ReadAllText(filePath);
                var commands = ParseJson(json);
                writer.ImportCommands(commands, platform);
            }

            writer.Commit();
        }

        /// <summary>
        /// Imports a single legacy settings JSON string for a given platform.
        /// </summary>
        internal static void ImportJson(CommandDatabaseWriter writer, string json, PlatformInfo platform)
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
        /// Parses a filename like "core-6.1.0-windows" into a <see cref="PlatformInfo"/>.
        /// Legacy filenames do not encode OS version, SKU, or environment.
        /// </summary>
        internal static bool TryParsePlatformFromFileName(string fileName, out PlatformInfo? platform)
        {
            platform = null;

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

            string edition = fileName.Substring(0, firstDash);
            string versionStr = fileName.Substring(firstDash + 1, lastDash - firstDash - 1);
            string osFamily = fileName.Substring(lastDash + 1);

            if (string.IsNullOrEmpty(edition) || string.IsNullOrEmpty(versionStr) || string.IsNullOrEmpty(osFamily))
            {
                return false;
            }

            if (string.Equals(edition, "core", StringComparison.OrdinalIgnoreCase))
            {
                edition = "Core";
            }
            else if (string.Equals(edition, "desktop", StringComparison.OrdinalIgnoreCase))
            {
                edition = "Desktop";
            }

            if (string.Equals(osFamily, "windows", StringComparison.OrdinalIgnoreCase))
            {
                osFamily = "Windows";
            }
            else if (string.Equals(osFamily, "linux", StringComparison.OrdinalIgnoreCase))
            {
                osFamily = "Linux";
            }
            else if (string.Equals(osFamily, "macos", StringComparison.OrdinalIgnoreCase))
            {
                osFamily = "MacOS";
            }

            platform = PlatformInfo.Create(edition, versionStr, new OsInfo(osFamily));
            return true;
        }
    }
}
