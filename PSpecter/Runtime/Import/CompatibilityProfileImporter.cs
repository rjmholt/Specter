using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace PSpecter.Runtime.Import
{
    /// <summary>
    /// Imports command metadata from PSCompatibilityCollector JSON profile files.
    /// These profiles contain rich parameter/parameter-set/alias/output-type data
    /// along with platform identification.
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

            using var writer = new CommandDatabaseWriter(connection);
            writer.BeginTransaction();

            foreach (string filePath in Directory.GetFiles(profileDirectory, "*.json"))
            {
                string json = File.ReadAllText(filePath);
                ImportJson(writer, json);
            }

            writer.CommitTransaction();
        }

        /// <summary>
        /// Imports a single compatibility profile JSON string.
        /// </summary>
        public static void ImportJson(CommandDatabaseWriter writer, string json)
        {
            JObject root = JObject.Parse(json);

            long platformId = ExtractAndEnsurePlatform(writer, root);

            JObject runtime = (JObject)root["Runtime"];
            if (runtime is null) return;

            JObject modules = (JObject)runtime["Modules"];
            if (modules is null) return;

            foreach (var moduleEntry in modules)
            {
                string moduleName = moduleEntry.Key;
                JObject versions = (JObject)moduleEntry.Value;
                if (versions is null) continue;

                foreach (var versionEntry in versions)
                {
                    string moduleVersion = versionEntry.Key;
                    JObject moduleData = (JObject)versionEntry.Value;
                    if (moduleData is null) continue;

                    long moduleId = writer.EnsureModule(moduleName, moduleVersion);

                    ImportCmdlets(writer, moduleData, moduleId, platformId);
                    ImportFunctions(writer, moduleData, moduleId, platformId);
                    ImportModuleAliases(writer, moduleData, moduleId, platformId);
                }
            }
        }

        private static long ExtractAndEnsurePlatform(CommandDatabaseWriter writer, JObject root)
        {
            JObject platform = (JObject)root["Platform"];
            string edition = "Core";
            string version = "0.0.0";
            string os = "windows";

            if (platform is not null)
            {
                JObject ps = (JObject)platform["PowerShell"];
                if (ps is not null)
                {
                    edition = (string)ps["Edition"] ?? "Core";
                    JToken versionToken = ps["Version"];
                    if (versionToken is not null)
                    {
                        version = ExtractVersionString(versionToken);
                    }
                }

                JObject osData = (JObject)platform["OperatingSystem"];
                if (osData is not null)
                {
                    os = NormalizeOsFamily((string)osData["Family"]);
                }
            }

            return writer.EnsurePlatform(NormalizeEdition(edition), version, os);
        }

        private static void ImportCmdlets(CommandDatabaseWriter writer, JObject moduleData, long moduleId, long platformId)
        {
            JObject cmdlets = (JObject)moduleData["Cmdlets"];
            if (cmdlets is null) return;

            foreach (var entry in cmdlets)
            {
                ImportCommand(writer, entry.Key, "Cmdlet", (JObject)entry.Value, moduleId, platformId);
            }
        }

        private static void ImportFunctions(CommandDatabaseWriter writer, JObject moduleData, long moduleId, long platformId)
        {
            JObject functions = (JObject)moduleData["Functions"];
            if (functions is null) return;

            foreach (var entry in functions)
            {
                ImportCommand(writer, entry.Key, "Function", (JObject)entry.Value, moduleId, platformId);
            }
        }

        private static void ImportCommand(
            CommandDatabaseWriter writer,
            string commandName,
            string commandType,
            JObject commandData,
            long moduleId,
            long platformId)
        {
            string defaultParameterSet = (string)commandData["DefaultParameterSet"];

            long? existingId = writer.FindCommand(moduleId, commandName);
            long commandId;
            if (existingId.HasValue)
            {
                commandId = existingId.Value;
            }
            else
            {
                commandId = writer.InsertCommand(moduleId, commandName, commandType, defaultParameterSet);
            }

            writer.LinkCommandPlatform(commandId, platformId);

            ImportParameters(writer, commandData, commandId, platformId);
            ImportOutputTypes(writer, commandData, commandId);
        }

        private static void ImportParameters(CommandDatabaseWriter writer, JObject commandData, long commandId, long platformId)
        {
            JObject parameters = (JObject)commandData["Parameters"];
            if (parameters is null) return;

            foreach (var paramEntry in parameters)
            {
                string paramName = paramEntry.Key;
                JObject paramData = (JObject)paramEntry.Value;
                if (paramData is null) continue;

                string paramType = (string)paramData["Type"];
                bool isDynamic = (bool?)paramData["Dynamic"] ?? false;

                long? existingParamId = writer.FindParameter(commandId, paramName);
                long paramId;
                if (existingParamId.HasValue)
                {
                    paramId = existingParamId.Value;
                }
                else
                {
                    paramId = writer.InsertParameter(commandId, paramName, paramType, isDynamic);
                }

                writer.LinkParameterPlatform(paramId, platformId);

                ImportParameterSets(writer, paramData, paramId);
            }
        }

        private static void ImportParameterSets(CommandDatabaseWriter writer, JObject paramData, long paramId)
        {
            JObject parameterSets = (JObject)paramData["ParameterSets"];
            if (parameterSets is null) return;

            foreach (var setEntry in parameterSets)
            {
                string setName = setEntry.Key;
                JObject setData = (JObject)setEntry.Value;
                if (setData is null) continue;

                int? position = null;
                JToken posToken = setData["Position"];
                if (posToken is not null && posToken.Type == JTokenType.Integer)
                {
                    int pos = (int)posToken;
                    if (pos != int.MinValue)
                    {
                        position = pos;
                    }
                }

                bool isMandatory = false;
                bool valueFromPipeline = false;
                bool valueFromPipelineByPropertyName = false;

                JArray flags = (JArray)setData["Flags"];
                if (flags is not null)
                {
                    foreach (JToken flag in flags)
                    {
                        string flagStr = flag.ToString();
                        if (string.Equals(flagStr, "Mandatory", StringComparison.OrdinalIgnoreCase))
                            isMandatory = true;
                        else if (string.Equals(flagStr, "ValueFromPipeline", StringComparison.OrdinalIgnoreCase))
                            valueFromPipeline = true;
                        else if (string.Equals(flagStr, "ValueFromPipelineByPropertyName", StringComparison.OrdinalIgnoreCase))
                            valueFromPipelineByPropertyName = true;
                    }
                }

                writer.InsertParameterSetMembership(paramId, setName, position,
                    isMandatory, valueFromPipeline, valueFromPipelineByPropertyName);
            }
        }

        private static void ImportOutputTypes(CommandDatabaseWriter writer, JObject commandData, long commandId)
        {
            JArray outputTypes = (JArray)commandData["OutputType"];
            if (outputTypes is null) return;

            foreach (JToken typeToken in outputTypes)
            {
                string typeName = typeToken.ToString();
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    writer.InsertOutputType(commandId, typeName);
                }
            }
        }

        private static void ImportModuleAliases(CommandDatabaseWriter writer, JObject moduleData, long moduleId, long platformId)
        {
            JObject aliases = (JObject)moduleData["Aliases"];
            if (aliases is null) return;

            foreach (var aliasEntry in aliases)
            {
                string aliasName = aliasEntry.Key;
                string targetCommand = (string)aliasEntry.Value;

                if (string.IsNullOrWhiteSpace(aliasName)) continue;

                // Find the target command in the same module
                long? targetId = null;
                if (!string.IsNullOrWhiteSpace(targetCommand))
                {
                    targetId = writer.FindCommand(moduleId, targetCommand);
                }

                long commandId;
                if (targetId.HasValue)
                {
                    commandId = targetId.Value;
                }
                else
                {
                    // Target command not in this module; create a placeholder command entry
                    long? existing = writer.FindCommand(moduleId, aliasName);
                    if (existing.HasValue)
                    {
                        commandId = existing.Value;
                    }
                    else
                    {
                        commandId = writer.InsertCommand(moduleId, targetCommand ?? aliasName, "Alias", null);
                    }
                    writer.LinkCommandPlatform(commandId, platformId);
                }

                long? existingAlias = writer.FindAlias(aliasName, commandId);
                long aliasId;
                if (existingAlias.HasValue)
                {
                    aliasId = existingAlias.Value;
                }
                else
                {
                    aliasId = writer.InsertAlias(aliasName, commandId);
                }
                writer.LinkAliasPlatform(aliasId, platformId);
            }
        }

        private static string ExtractVersionString(JToken versionToken)
        {
            if (versionToken.Type == JTokenType.String)
            {
                return (string)versionToken;
            }

            if (versionToken.Type == JTokenType.Object)
            {
                JObject vObj = (JObject)versionToken;
                int major = (int?)vObj["Major"] ?? 0;
                int minor = (int?)vObj["Minor"] ?? 0;
                int patch = (int?)vObj["Patch"] ?? (int?)vObj["Build"] ?? 0;
                JToken label = vObj["Label"] ?? vObj["PreReleaseLabel"];

                string ver = $"{major}.{minor}.{patch}";
                if (label is not null)
                {
                    string labelStr = label.ToString();
                    if (!string.IsNullOrWhiteSpace(labelStr))
                    {
                        ver += $"-{labelStr}";
                    }
                }
                return ver;
            }

            return versionToken.ToString();
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
