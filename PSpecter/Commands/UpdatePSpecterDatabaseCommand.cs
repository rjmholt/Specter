using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Microsoft.Data.Sqlite;
using PSpecter.Runtime;
using PSpecter.Runtime.Import;
using ParameterMetadata2 = System.Management.Automation.ParameterMetadata;

namespace PSpecter.Commands
{
    /// <summary>
    /// Cmdlet to create or update the PSpecter command database.
    /// Supports collecting data from the current PowerShell session
    /// or importing from JSON files in legacy or compatibility profile formats.
    /// </summary>
    [Cmdlet(VerbsData.Update, "PSpecterDatabase", DefaultParameterSetName = "Session")]
    public class UpdatePSpecterDatabaseCommand : PSCmdlet
    {
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty]
        public string DatabasePath { get; set; }

        [Parameter(ParameterSetName = "Session")]
        public SwitchParameter FromSession { get; set; }

        [Parameter(ParameterSetName = "LegacyJson", Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string LegacySettingsPath { get; set; }

        [Parameter(ParameterSetName = "CompatibilityProfile", Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string CompatibilityProfilePath { get; set; }

        [Parameter(ParameterSetName = "Session")]
        [ValidateNotNullOrEmpty]
        public string Edition { get; set; }

        [Parameter(ParameterSetName = "Session")]
        [ValidateNotNullOrEmpty]
        public string PlatformOS { get; set; }

        protected override void EndProcessing()
        {
            string dbPath = ResolveDatabasePath();
            bool isNew = !File.Exists(dbPath);

            WriteVerbose($"Database path: {dbPath}");

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = isNew ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite
            }.ToString());
            connection.Open();

            if (isNew)
            {
                WriteVerbose("Creating new database schema...");
                CommandDatabaseSchema.CreateTables(connection);
                using var writer = new CommandDatabaseWriter(connection);
                writer.WriteSchemaVersion(CommandDatabaseSchema.SchemaVersion);
            }

            switch (ParameterSetName)
            {
                case "Session":
                    CollectFromSession(connection);
                    break;

                case "LegacyJson":
                    ImportLegacyJson(connection);
                    break;

                case "CompatibilityProfile":
                    ImportCompatibilityProfile(connection);
                    break;
            }

            WriteObject(new FileInfo(dbPath));
        }

        private void CollectFromSession(SqliteConnection connection)
        {
            WriteVerbose("Collecting command data from current session...");

            string edition = Edition ?? GetSessionEdition();
            string version = GetSessionVersion();
            string os = PlatformOS ?? GetSessionOS();

            WriteVerbose($"Platform: {edition}/{version}/{os}");

            using var writer = new CommandDatabaseWriter(connection);
            writer.BeginTransaction();

            long platformId = writer.EnsurePlatform(edition, version, os);

            var commands = InvokeCommand.InvokeScript(
                "Get-Command -CommandType Cmdlet,Function -ErrorAction SilentlyContinue");

            int commandCount = 0;
            foreach (PSObject cmdObj in commands)
            {
                if (cmdObj.BaseObject is CommandInfo cmdInfo)
                {
                    ImportCommandInfo(writer, cmdInfo, platformId);
                    commandCount++;
                }
            }

            ImportAliases(writer, platformId);

            writer.CommitTransaction();
            WriteVerbose($"Imported {commandCount} commands.");
        }

        private void ImportCommandInfo(CommandDatabaseWriter writer, CommandInfo cmdInfo, long platformId)
        {
            string moduleName = cmdInfo.ModuleName;
            if (string.IsNullOrEmpty(moduleName))
            {
                moduleName = "(none)";
            }

            string moduleVersion = cmdInfo.Module?.Version?.ToString();
            long moduleId = writer.EnsureModule(moduleName, moduleVersion);

            string commandType = cmdInfo.CommandType.ToString();
            string defaultParamSet = null;

            if (cmdInfo is CmdletInfo cmdletInfo)
            {
                defaultParamSet = cmdletInfo.DefaultParameterSet;
            }
            else if (cmdInfo is FunctionInfo funcInfo)
            {
                defaultParamSet = funcInfo.DefaultParameterSet;
            }

            long? existingId = writer.FindCommand(moduleId, cmdInfo.Name);
            long commandId;
            if (existingId.HasValue)
            {
                commandId = existingId.Value;
            }
            else
            {
                commandId = writer.InsertCommand(moduleId, cmdInfo.Name, commandType, defaultParamSet);
            }

            writer.LinkCommandPlatform(commandId, platformId);

            try
            {
                foreach (var paramEntry in cmdInfo.Parameters)
                {
                    string paramName = paramEntry.Key;
                    ParameterMetadata2 paramMeta = paramEntry.Value;

                    long? existingParam = writer.FindParameter(commandId, paramName);
                    long paramId;
                    if (existingParam.HasValue)
                    {
                        paramId = existingParam.Value;
                    }
                    else
                    {
                        paramId = writer.InsertParameter(
                            commandId,
                            paramName,
                            paramMeta.ParameterType?.FullName,
                            paramMeta.IsDynamic);
                    }

                    writer.LinkParameterPlatform(paramId, platformId);

                    foreach (var setEntry in paramMeta.ParameterSets)
                    {
                        ParameterSetMetadata setMeta = setEntry.Value;
                        writer.InsertParameterSetMembership(
                            paramId,
                            setEntry.Key,
                            setMeta.Position == int.MinValue ? (int?)null : setMeta.Position,
                            setMeta.IsMandatory,
                            setMeta.ValueFromPipeline,
                            setMeta.ValueFromPipelineByPropertyName);
                    }
                }
            }
            catch (RuntimeException)
            {
                // Some commands throw when accessing Parameters (e.g. Get-WmiObject without WMI)
            }

            try
            {
                foreach (var outputType in cmdInfo.OutputType)
                {
                    string typeName = outputType.Type?.FullName ?? outputType.Name;
                    if (!string.IsNullOrWhiteSpace(typeName))
                    {
                        writer.InsertOutputType(commandId, typeName);
                    }
                }
            }
            catch (RuntimeException)
            {
            }
        }

        private void ImportAliases(CommandDatabaseWriter writer, long platformId)
        {
            var aliases = InvokeCommand.InvokeScript(
                "Get-Alias -ErrorAction SilentlyContinue");

            int aliasCount = 0;
            foreach (PSObject aliasObj in aliases)
            {
                if (aliasObj.BaseObject is not AliasInfo aliasInfo) continue;

                string targetName = aliasInfo.Definition;
                if (string.IsNullOrEmpty(targetName)) continue;

                // Find the target command's module and ID
                string moduleName = aliasInfo.Module?.Name;
                if (string.IsNullOrEmpty(moduleName))
                {
                    moduleName = "(none)";
                }

                long moduleId = writer.EnsureModule(moduleName, aliasInfo.Module?.Version?.ToString());

                // Try to find the target command already inserted
                long? targetCmdId = writer.FindCommand(moduleId, targetName);
                if (!targetCmdId.HasValue)
                {
                    // The target command might be in a different module; try a broader search
                    // by inserting a placeholder "Alias" command entry
                    targetCmdId = writer.InsertCommand(moduleId, targetName, "Alias", null);
                    writer.LinkCommandPlatform(targetCmdId.Value, platformId);
                }

                long? existingAlias = writer.FindAlias(aliasInfo.Name, targetCmdId.Value);
                long aliasId = existingAlias ?? writer.InsertAlias(aliasInfo.Name, targetCmdId.Value);
                writer.LinkAliasPlatform(aliasId, platformId);
                aliasCount++;
            }

            WriteVerbose($"Imported {aliasCount} aliases.");
        }

        private void ImportLegacyJson(SqliteConnection connection)
        {
            string path = GetUnresolvedProviderPathFromPSPath(LegacySettingsPath);

            if (Directory.Exists(path))
            {
                WriteVerbose($"Importing legacy settings from directory: {path}");
                LegacySettingsImporter.ImportDirectory(connection, path);
            }
            else if (File.Exists(path))
            {
                WriteVerbose($"Importing legacy settings from file: {path}");
                string json = File.ReadAllText(path);
                string fileName = Path.GetFileNameWithoutExtension(path);

                using var writer = new CommandDatabaseWriter(connection);
                writer.BeginTransaction();

                if (LegacySettingsImporter.TryParsePlatformFromFileName(
                    fileName, out string edition, out string version, out string os))
                {
                    long platformId = writer.EnsurePlatform(edition, version, os);
                    LegacySettingsImporter.ImportJson(writer, json, platformId);
                }
                else
                {
                    WriteWarning($"Could not parse platform from filename '{fileName}'. Using defaults.");
                    long platformId = writer.EnsurePlatform("Core", "0.0.0", "unknown");
                    LegacySettingsImporter.ImportJson(writer, json, platformId);
                }

                writer.CommitTransaction();
            }
            else
            {
                ThrowTerminatingError(new ErrorRecord(
                    new FileNotFoundException($"Path not found: {path}"),
                    "PathNotFound",
                    ErrorCategory.ObjectNotFound,
                    path));
            }
        }

        private void ImportCompatibilityProfile(SqliteConnection connection)
        {
            string path = GetUnresolvedProviderPathFromPSPath(CompatibilityProfilePath);

            if (Directory.Exists(path))
            {
                WriteVerbose($"Importing compatibility profiles from directory: {path}");
                CompatibilityProfileImporter.ImportDirectory(connection, path);
            }
            else if (File.Exists(path))
            {
                WriteVerbose($"Importing compatibility profile from file: {path}");
                string json = File.ReadAllText(path);
                using var writer = new CommandDatabaseWriter(connection);
                writer.BeginTransaction();
                CompatibilityProfileImporter.ImportJson(writer, json);
                writer.CommitTransaction();
            }
            else
            {
                ThrowTerminatingError(new ErrorRecord(
                    new FileNotFoundException($"Path not found: {path}"),
                    "PathNotFound",
                    ErrorCategory.ObjectNotFound,
                    path));
            }
        }

        private string ResolveDatabasePath()
        {
            if (!string.IsNullOrEmpty(DatabasePath))
            {
                return GetUnresolvedProviderPathFromPSPath(DatabasePath);
            }

            // Default to the module's data directory
            string moduleBase = MyInvocation.MyCommand.Module?.ModuleBase;
            if (moduleBase is not null)
            {
                return System.IO.Path.Combine(moduleBase, "Data", "pspecter.db");
            }

            // Fallback: current directory
            return System.IO.Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, "pspecter.db");
        }

        private string GetSessionEdition()
        {
            var result = InvokeCommand.InvokeScript("$PSVersionTable.PSEdition");
            return result.Count > 0 ? result[0]?.ToString() ?? "Core" : "Core";
        }

        private string GetSessionVersion()
        {
            var result = InvokeCommand.InvokeScript("$PSVersionTable.PSVersion.ToString()");
            return result.Count > 0 ? result[0]?.ToString() ?? "0.0.0" : "0.0.0";
        }

        private string GetSessionOS()
        {
#if CORECLR
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
                return "windows";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
                return "macos";
            return "linux";
#else
            return "windows";
#endif
        }
    }
}
