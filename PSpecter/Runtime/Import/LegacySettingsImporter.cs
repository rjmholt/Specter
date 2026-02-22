using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace PSpecter.Runtime.Import
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

            using var writer = new CommandDatabaseWriter(connection);
            writer.BeginTransaction();

            foreach (string filePath in Directory.GetFiles(settingsDirectory, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!TryParsePlatformFromFileName(fileName, out string edition, out string version, out string os))
                {
                    continue;
                }

                long platformId = writer.EnsurePlatform(edition, version, os);
                string json = File.ReadAllText(filePath);
                ImportJson(writer, json, platformId);
            }

            writer.CommitTransaction();
        }

        /// <summary>
        /// Imports a single legacy settings JSON string for a given platform.
        /// </summary>
        public static void ImportJson(CommandDatabaseWriter writer, string json, long platformId)
        {
            JObject root = JObject.Parse(json);
            JArray modules = (JArray)root["Modules"];
            if (modules is null) return;

            foreach (JObject module in modules)
            {
                string moduleName = (string)module["Name"];
                string moduleVersion = (string)module["Version"];

                long moduleId = writer.EnsureModule(moduleName, moduleVersion);

                ImportCommands(writer, module, moduleId, platformId);
                ImportAliases(writer, module, moduleId, platformId);
            }
        }

        private static void ImportCommands(CommandDatabaseWriter writer, JObject module, long moduleId, long platformId)
        {
            JArray commands = (JArray)module["ExportedCommands"];
            if (commands is null) return;

            foreach (JObject command in commands)
            {
                string name = (string)command["Name"];
                string commandType = (string)command["CommandType"] ?? "Cmdlet";

                long? existingId = writer.FindCommand(moduleId, name);
                long commandId;
                if (existingId.HasValue)
                {
                    commandId = existingId.Value;
                }
                else
                {
                    commandId = writer.InsertCommand(moduleId, name, commandType, defaultParameterSet: null);
                }

                writer.LinkCommandPlatform(commandId, platformId);
            }
        }

        private static void ImportAliases(CommandDatabaseWriter writer, JObject module, long moduleId, long platformId)
        {
            JArray aliases = (JArray)module["ExportedAliases"];
            if (aliases is null) return;

            // Legacy format only has alias names, no target mapping.
            // We create alias entries linked to a placeholder command (the module itself).
            // These can be enriched later from richer data sources.
            foreach (JToken aliasToken in aliases)
            {
                string aliasName = aliasToken.ToString();
                if (string.IsNullOrWhiteSpace(aliasName)) continue;

                // Try to find a matching command in the same module by looking at all commands.
                // Since legacy format doesn't have the mapping, we just record the alias
                // without a command link -- we use a sentinel "alias" command entry.
                long? sentinelId = writer.FindCommand(moduleId, aliasName);
                if (!sentinelId.HasValue)
                {
                    sentinelId = writer.InsertCommand(moduleId, aliasName, "Alias", defaultParameterSet: null);
                }

                writer.LinkCommandPlatform(sentinelId.Value, platformId);
            }
        }

        /// <summary>
        /// Parses a filename like "core-6.1.0-windows" into platform components.
        /// </summary>
        internal static bool TryParsePlatformFromFileName(string fileName, out string edition, out string version, out string os)
        {
            edition = null;
            version = null;
            os = null;

            // Format: {edition}-{version}-{os}
            // version may contain dots and hyphens (e.g. "5.1.14393.206")
            // OS is always the last segment
            int firstDash = fileName.IndexOf('-');
            if (firstDash < 0) return false;

            int lastDash = fileName.LastIndexOf('-');
            if (lastDash <= firstDash) return false;

            edition = fileName.Substring(0, firstDash);
            version = fileName.Substring(firstDash + 1, lastDash - firstDash - 1);
            os = fileName.Substring(lastDash + 1);

            // Normalize edition
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
