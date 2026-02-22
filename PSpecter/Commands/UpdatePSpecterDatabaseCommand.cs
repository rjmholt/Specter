using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Microsoft.Data.Sqlite;
using PSpecter.CommandDatabase;
using PSpecter.CommandDatabase.Import;
using PSpecter.CommandDatabase.Sqlite;
using PsCommandMetadata = PSpecter.CommandDatabase.CommandMetadata;
using PsParameterMetadata = PSpecter.CommandDatabase.ParameterMetadata;
using SmaParameterMetadata = System.Management.Automation.ParameterMetadata;

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
                using var writer = CommandDatabaseWriter.Begin(connection);
                writer.WriteSchemaVersion(CommandDatabaseSchema.SchemaVersion);
                writer.Commit();
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

            var platform = new PlatformInfo(edition, version, os);
            var commands = new List<PsCommandMetadata>();

            var psCommands = InvokeCommand.InvokeScript(
                "Get-Command -CommandType Cmdlet,Function -ErrorAction SilentlyContinue");

            foreach (PSObject cmdObj in psCommands)
            {
                if (cmdObj.BaseObject is CommandInfo cmdInfo)
                {
                    PsCommandMetadata meta = ConvertCommandInfo(cmdInfo);
                    if (meta is not null)
                        commands.Add(meta);
                }
            }

            // Collect aliases separately and add them as commands with alias info
            CollectAliases(commands);

            using var writer = CommandDatabaseWriter.Begin(connection);
            writer.ImportCommands(commands, platform);
            writer.Commit();

            WriteVerbose($"Imported {commands.Count} commands.");
        }

        private PsCommandMetadata ConvertCommandInfo(CommandInfo cmdInfo)
        {
            string moduleName = cmdInfo.ModuleName;
            if (string.IsNullOrEmpty(moduleName))
                moduleName = "(none)";

            string commandType = cmdInfo.CommandType.ToString();
            string defaultParamSet = null;

            if (cmdInfo is CmdletInfo cmdletInfo)
                defaultParamSet = cmdletInfo.DefaultParameterSet;
            else if (cmdInfo is FunctionInfo funcInfo)
                defaultParamSet = funcInfo.DefaultParameterSet;

            var parameters = new List<PsParameterMetadata>();
            try
            {
                if (cmdInfo.Parameters is not null)
                {
                    foreach (var paramEntry in cmdInfo.Parameters)
                    {
                        SmaParameterMetadata paramMeta = paramEntry.Value;
                        var sets = new List<ParameterSetInfo>();

                        foreach (var setEntry in paramMeta.ParameterSets)
                        {
                            ParameterSetMetadata setMeta = setEntry.Value;
                            int? position = setMeta.Position == int.MinValue ? null : setMeta.Position;
                            sets.Add(new ParameterSetInfo(
                                setEntry.Key,
                                position,
                                setMeta.IsMandatory,
                                setMeta.ValueFromPipeline,
                                setMeta.ValueFromPipelineByPropertyName));
                        }

                        parameters.Add(new PsParameterMetadata(
                            paramEntry.Key,
                            paramMeta.ParameterType?.FullName,
                            paramMeta.IsDynamic,
                            sets));
                    }
                }
            }
            catch (RuntimeException)
            {
            }

            var outputTypes = new List<string>();
            try
            {
                foreach (var outputType in cmdInfo.OutputType)
                {
                    string typeName = outputType.Type?.FullName ?? outputType.Name;
                    if (!string.IsNullOrWhiteSpace(typeName))
                        outputTypes.Add(typeName);
                }
            }
            catch (RuntimeException)
            {
            }

            return new PsCommandMetadata(
                cmdInfo.Name,
                commandType,
                moduleName,
                defaultParamSet,
                parameterSetNames: null,
                aliases: null,
                parameters: parameters,
                outputTypes: outputTypes);
        }

        private void CollectAliases(List<PsCommandMetadata> commands)
        {
            var aliases = InvokeCommand.InvokeScript(
                "Get-Alias -ErrorAction SilentlyContinue");

            foreach (PSObject aliasObj in aliases)
            {
                if (aliasObj.BaseObject is not AliasInfo aliasInfo) continue;

                string targetName = aliasInfo.Definition;
                if (string.IsNullOrEmpty(targetName)) continue;

                string moduleName = aliasInfo.Module?.Name;
                if (string.IsNullOrEmpty(moduleName))
                    moduleName = "(none)";

                commands.Add(new PsCommandMetadata(
                    name: targetName,
                    commandType: "Alias",
                    moduleName: moduleName,
                    defaultParameterSet: null,
                    parameterSetNames: null,
                    aliases: new[] { aliasInfo.Name },
                    parameters: null,
                    outputTypes: null));
            }
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

                if (!LegacySettingsImporter.TryParsePlatformFromFileName(
                    fileName, out string edition, out string version, out string os))
                {
                    WriteWarning($"Could not parse platform from filename '{fileName}'. Using defaults.");
                    edition = "Core";
                    version = "0.0.0";
                    os = "unknown";
                }

                var platform = new PlatformInfo(edition, version, os);
                using var writer = CommandDatabaseWriter.Begin(connection);
                LegacySettingsImporter.ImportJson(writer, json, platform);
                writer.Commit();
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
                using var writer = CommandDatabaseWriter.Begin(connection);
                CompatibilityProfileImporter.ImportJson(writer, json);
                writer.Commit();
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

            string moduleBase = MyInvocation.MyCommand.Module?.ModuleBase;
            if (moduleBase is not null)
            {
                return Path.Combine(moduleBase, "Data", "pspecter.db");
            }

            return Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, "pspecter.db");
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
