using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using PSpecter.CommandDatabase.Import.CompatibilityProfile;
using PSpecter.CommandDatabase.Sqlite;

namespace PSpecter.CommandDatabase.Import
{
    /// <summary>
    /// Imports command metadata from PSCompatibilityCollector JSON profile files.
    /// Deserializes into typed DTOs and converts to <see cref="CommandMetadata"/>
    /// for the coarse-grained writer API.
    /// </summary>
    public static class CompatibilityProfileImporter
    {
        /// <summary>
        /// Imports all JSON profiles from a directory.
        /// </summary>
        public static void ImportDirectory(SqliteConnection connection, string profileDirectory)
        {
            if (!Directory.Exists(profileDirectory))
            {
                throw new DirectoryNotFoundException($"Profile directory not found: {profileDirectory}");
            }

            using var writer = CommandDatabaseWriter.Begin(connection);

            foreach (string filePath in Directory.GetFiles(profileDirectory, "*.json"))
            {
                string json = File.ReadAllText(filePath);
                var (platform, commands) = ParseJson(json);
                writer.ImportCommands(commands, platform);
            }

            writer.Commit();
        }

        /// <summary>
        /// Imports a single compatibility profile JSON string.
        /// </summary>
        public static void ImportJson(CommandDatabaseWriter writer, string json)
        {
            var (platform, commands) = ParseJson(json);
            writer.ImportCommands(commands, platform);
        }

        /// <summary>
        /// Parses a compatibility profile JSON string into platform info and command metadata.
        /// </summary>
        internal static (PlatformInfo Platform, IReadOnlyList<CommandMetadata> Commands) ParseJson(string json)
        {
            var root = JsonConvert.DeserializeObject<ProfileRoot>(json);

            PlatformInfo platform = ExtractPlatform(root);

            var commands = new List<CommandMetadata>();

            if (root?.Runtime?.Modules is null)
                return (platform, commands);

            foreach (var moduleEntry in root.Runtime.Modules)
            {
                string moduleName = moduleEntry.Key;
                if (moduleEntry.Value is null) continue;

                foreach (var versionEntry in moduleEntry.Value)
                {
                    ModuleVersion moduleData = versionEntry.Value;
                    if (moduleData is null) continue;

                    var aliasMap = moduleData.Aliases ?? new Dictionary<string, string>();

                    var commandAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in aliasMap)
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                            continue;

                        if (!commandAliases.TryGetValue(kvp.Value, out var list))
                        {
                            list = new List<string>();
                            commandAliases[kvp.Value] = list;
                        }
                        list.Add(kvp.Key);
                    }

                    if (moduleData.Cmdlets is not null)
                    {
                        foreach (var cmdEntry in moduleData.Cmdlets)
                        {
                            commands.Add(ConvertCommand(cmdEntry.Key, "Cmdlet", cmdEntry.Value, moduleName, commandAliases));
                        }
                    }

                    if (moduleData.Functions is not null)
                    {
                        foreach (var funcEntry in moduleData.Functions)
                        {
                            commands.Add(ConvertCommand(funcEntry.Key, "Function", funcEntry.Value, moduleName, commandAliases));
                        }
                    }

                    foreach (var aliasKvp in aliasMap)
                    {
                        if (string.IsNullOrWhiteSpace(aliasKvp.Key)) continue;
                        string targetName = aliasKvp.Value;

                        bool alreadyHandled = false;
                        if (moduleData.Cmdlets is not null && moduleData.Cmdlets.ContainsKey(targetName))
                            alreadyHandled = true;
                        if (!alreadyHandled && moduleData.Functions is not null && moduleData.Functions.ContainsKey(targetName))
                            alreadyHandled = true;

                        if (!alreadyHandled)
                        {
                            commands.Add(new CommandMetadata(
                                name: targetName ?? aliasKvp.Key,
                                commandType: "Alias",
                                moduleName: moduleName,
                                defaultParameterSet: null,
                                parameterSetNames: null,
                                aliases: new[] { aliasKvp.Key },
                                parameters: null,
                                outputTypes: null));
                        }
                    }
                }
            }

            return (platform, commands);
        }

        private static CommandMetadata ConvertCommand(
            string commandName,
            string commandType,
            CommandData data,
            string moduleName,
            Dictionary<string, List<string>> aliasLookup)
        {
            var parameters = new List<ParameterMetadata>();
            if (data?.Parameters is not null)
            {
                foreach (var paramEntry in data.Parameters)
                {
                    ParameterData paramData = paramEntry.Value;
                    if (paramData is null) continue;

                    var sets = new List<ParameterSetInfo>();
                    if (paramData.ParameterSets is not null)
                    {
                        foreach (var setEntry in paramData.ParameterSets)
                        {
                            ParameterSetData setData = setEntry.Value;
                            if (setData is null) continue;

                            int? position = setData.Position;
                            if (position == int.MinValue)
                                position = null;

                            bool isMandatory = false;
                            bool valueFromPipeline = false;
                            bool valueFromPipelineByPropertyName = false;

                            if (setData.Flags is not null)
                            {
                                foreach (string flag in setData.Flags)
                                {
                                    if (string.Equals(flag, "Mandatory", StringComparison.OrdinalIgnoreCase))
                                        isMandatory = true;
                                    else if (string.Equals(flag, "ValueFromPipeline", StringComparison.OrdinalIgnoreCase))
                                        valueFromPipeline = true;
                                    else if (string.Equals(flag, "ValueFromPipelineByPropertyName", StringComparison.OrdinalIgnoreCase))
                                        valueFromPipelineByPropertyName = true;
                                }
                            }

                            sets.Add(new ParameterSetInfo(
                                setEntry.Key, position, isMandatory, valueFromPipeline, valueFromPipelineByPropertyName));
                        }
                    }

                    parameters.Add(new ParameterMetadata(paramEntry.Key, paramData.Type, paramData.Dynamic, sets));
                }
            }

            List<string> aliases = null;
            if (aliasLookup.TryGetValue(commandName, out var aliasList))
            {
                aliases = aliasList;
            }

            return new CommandMetadata(
                name: commandName,
                commandType: commandType,
                moduleName: moduleName,
                defaultParameterSet: data?.DefaultParameterSet,
                parameterSetNames: data?.ParameterSets,
                aliases: aliases,
                parameters: parameters,
                outputTypes: data?.OutputType);
        }

        private static PlatformInfo ExtractPlatform(ProfileRoot root)
        {
            string edition = "Core";
            string version = "0.0.0";
            string os = "windows";

            if (root?.Platform is not null)
            {
                if (root.Platform.PowerShell is not null)
                {
                    edition = NormalizeEdition(root.Platform.PowerShell.Edition);
                    if (!string.IsNullOrEmpty(root.Platform.PowerShell.Version))
                        version = root.Platform.PowerShell.Version;
                }

                if (root.Platform.OperatingSystem is not null)
                {
                    os = NormalizeOsFamily(root.Platform.OperatingSystem.Family);
                }
            }

            return new PlatformInfo(edition, version, os);
        }

        private static string NormalizeEdition(string edition)
        {
            if (string.Equals(edition, "Core", StringComparison.OrdinalIgnoreCase)) return "Core";
            if (string.Equals(edition, "Desktop", StringComparison.OrdinalIgnoreCase)) return "Desktop";
            return edition ?? "Core";
        }

        private static string NormalizeOsFamily(string family)
        {
            if (family is null) return "windows";
            if (family.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0) return "windows";
            if (family.IndexOf("MacOS", StringComparison.OrdinalIgnoreCase) >= 0) return "macos";
            if (family.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) >= 0) return "linux";
            return family.ToLowerInvariant();
        }
    }
}
