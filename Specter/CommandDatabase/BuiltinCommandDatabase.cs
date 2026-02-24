using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Specter.CommandDatabase.Sqlite;

namespace Specter.CommandDatabase
{
    /// <summary>
    /// The default command database used by rules. When a shipped specter.db
    /// is available, it delegates rich queries to <see cref="SqliteCommandDatabase"/>.
    /// Hardcoded alias data serves as a last-resort fallback.
    /// </summary>
    public class BuiltinCommandDatabase : IPowerShellCommandDatabase, IDisposable
    {
        private static readonly Lazy<BuiltinCommandDatabase> s_instance =
            new Lazy<BuiltinCommandDatabase>(() => new BuiltinCommandDatabase());

        public static BuiltinCommandDatabase Instance => s_instance.Value;

        /// <summary>
        /// Override the default database path. When set, <see cref="Instance"/>
        /// will look here before probing assembly-relative locations.
        /// Must be set before the first access of <see cref="Instance"/>.
        /// </summary>
        public static string? DefaultDatabasePath { get; set; }

        private readonly SqliteCommandDatabase? _sqliteDb;
        private readonly Dictionary<string, string> _aliasToCommand;
        private readonly Dictionary<string, List<string>> _commandToAliases;

        private BuiltinCommandDatabase()
        {
            _aliasToCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _commandToAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            PopulateDefaultAliases();

            string? dbPath = FindDefaultDatabasePath();
            if (dbPath is not null)
            {
                try
                {
                    _sqliteDb = SqliteCommandDatabase.Open(dbPath);
                }
                catch
                {
                    // If the database can't be opened, fall back silently to hardcoded data
                }
            }
        }

        /// <summary>
        /// Creates a BuiltinCommandDatabase backed by a specific database file.
        /// Falls back to hardcoded aliases for anything the database doesn't cover.
        /// </summary>
        public static BuiltinCommandDatabase CreateWithDatabase(string databasePath)
        {
            return new BuiltinCommandDatabase(databasePath);
        }

        private BuiltinCommandDatabase(string databasePath)
        {
            _aliasToCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _commandToAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            PopulateDefaultAliases();

            if (databasePath is not null && File.Exists(databasePath))
            {
                _sqliteDb = SqliteCommandDatabase.Open(databasePath);
            }
        }

        public bool HasSqliteDatabase => _sqliteDb is not null;

        public bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo>? platforms, out CommandMetadata? command)
        {
            if (_sqliteDb is not null)
            {
                return _sqliteDb.TryGetCommand(nameOrAlias, platforms, out command);
            }

            command = null;
            return false;
        }

        public string? GetAliasTarget(string alias)
        {
            if (_sqliteDb is not null)
            {
                string? result = _sqliteDb.GetAliasTarget(alias);
                if (result is not null)
                {
                    return result;
                }
            }

            return _aliasToCommand.TryGetValue(alias, out string? target) ? target : null;
        }

        public IReadOnlyList<string>? GetCommandAliases(string command)
        {
            if (_sqliteDb is not null)
            {
                IReadOnlyList<string>? result = _sqliteDb.GetCommandAliases(command);
                if (result is not null)
                {
                    return result;
                }
            }

            return _commandToAliases.TryGetValue(command, out List<string>? aliases) ? aliases : null;
        }

        public IReadOnlyList<string> GetAllNamesForCommand(string command)
        {
            if (_sqliteDb is not null)
            {
                IReadOnlyList<string>? result = _sqliteDb.GetAllNamesForCommand(command);
                if (result is not null)
                {
                    return result;
                }
            }

            var names = new List<string> { command };
            if (_commandToAliases.TryGetValue(command, out List<string>? aliases) && aliases is not null)
            {
                names.AddRange(aliases);
            }
            return names;
        }

        public bool CommandExistsOnPlatform(string nameOrAlias, HashSet<PlatformInfo>? platforms)
        {
            if (_sqliteDb is not null)
            {
                return _sqliteDb.CommandExistsOnPlatform(nameOrAlias, platforms);
            }

            return _aliasToCommand.ContainsKey(nameOrAlias)
                || _commandToAliases.ContainsKey(nameOrAlias);
        }

        public bool TryResolveProfile(string profileName, out PlatformInfo? platform)
        {
            if (_sqliteDb is not null)
            {
                return _sqliteDb.TryResolveProfile(profileName, out platform);
            }

            platform = null;
            return false;
        }

        public void Dispose()
        {
            _sqliteDb?.Dispose();
        }

        /// <summary>
        /// Resolves the default database path by checking <see cref="DefaultDatabasePath"/>,
        /// then probing assembly-relative <c>Data/specter.db</c> locations.
        /// Returns null if no database file is found.
        /// </summary>
        public static string? FindDefaultDatabasePath()
        {
            try
            {
                if (DefaultDatabasePath is not null && File.Exists(DefaultDatabasePath))
                {
                    return DefaultDatabasePath;
                }

                string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (assemblyDir is null)
                {
                    return null;
                }

                string? candidate = TryResolveDatabasePath(assemblyDir);
                if (candidate is not null)
                {
                    return candidate;
                }

                string? parentDir = Path.GetDirectoryName(assemblyDir);
                if (parentDir is not null)
                {
                    candidate = TryResolveDatabasePath(parentDir);
                    if (candidate is not null)
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? TryResolveDatabasePath(string baseDir)
        {
            string specterDb = Path.Combine(baseDir, "Data", "specter.db");
            if (File.Exists(specterDb))
            {
                return specterDb;
            }

            return null;
        }

        private void AddAlias(string alias, string command)
        {
            _aliasToCommand[alias] = command;

            if (!_commandToAliases.TryGetValue(command, out List<string>? aliases))
            {
                aliases = new List<string>();
                _commandToAliases[command] = aliases;
            }

            aliases.Add(alias);
        }

        private void PopulateDefaultAliases()
        {
            AddAlias("?", "Where-Object");
            AddAlias("%", "ForEach-Object");
            AddAlias("cd", "Set-Location");
            AddAlias("chdir", "Set-Location");
            AddAlias("clc", "Clear-Content");
            AddAlias("clhy", "Clear-History");
            AddAlias("cli", "Clear-Item");
            AddAlias("clp", "Clear-ItemProperty");
            AddAlias("cls", "Clear-Host");
            AddAlias("clv", "Clear-Variable");
            AddAlias("copy", "Copy-Item");
            AddAlias("cpi", "Copy-Item");
            AddAlias("ctss", "ConvertTo-SecureString");
            AddAlias("cvpa", "Convert-Path");
            AddAlias("dbp", "Disable-PSBreakpoint");
            AddAlias("del", "Remove-Item");
            AddAlias("dir", "Get-ChildItem");
            AddAlias("ebp", "Enable-PSBreakpoint");
            AddAlias("echo", "Write-Output");
            AddAlias("epal", "Export-Alias");
            AddAlias("epcsv", "Export-Csv");
            AddAlias("erase", "Remove-Item");
            AddAlias("etsn", "Enter-PSSession");
            AddAlias("exsn", "Exit-PSSession");
            AddAlias("fc", "Format-Custom");
            AddAlias("fhx", "Format-Hex");
            AddAlias("fl", "Format-List");
            AddAlias("foreach", "ForEach-Object");
            AddAlias("ft", "Format-Table");
            AddAlias("fw", "Format-Wide");
            AddAlias("gal", "Get-Alias");
            AddAlias("gbp", "Get-PSBreakpoint");
            AddAlias("gc", "Get-Content");
            AddAlias("gcb", "Get-Clipboard");
            AddAlias("gci", "Get-ChildItem");
            AddAlias("gcm", "Get-Command");
            AddAlias("gcs", "Get-PSCallStack");
            AddAlias("gdr", "Get-PSDrive");
            AddAlias("gerr", "Get-Error");
            AddAlias("ghy", "Get-History");
            AddAlias("gi", "Get-Item");
            AddAlias("gjb", "Get-Job");
            AddAlias("gl", "Get-Location");
            AddAlias("gm", "Get-Member");
            AddAlias("gmo", "Get-Module");
            AddAlias("gp", "Get-ItemProperty");
            AddAlias("gps", "Get-Process");
            AddAlias("gpv", "Get-ItemPropertyValue");
            AddAlias("group", "Group-Object");
            AddAlias("gsn", "Get-PSSession");
            AddAlias("gtz", "Get-TimeZone");
            AddAlias("gu", "Get-Unique");
            AddAlias("gv", "Get-Variable");
            AddAlias("h", "Get-History");
            AddAlias("history", "Get-History");
            AddAlias("icm", "Invoke-Command");
            AddAlias("iex", "Invoke-Expression");
            AddAlias("ihy", "Invoke-History");
            AddAlias("ii", "Invoke-Item");
            AddAlias("ipal", "Import-Alias");
            AddAlias("ipcsv", "Import-Csv");
            AddAlias("ipmo", "Import-Module");
            AddAlias("irm", "Invoke-RestMethod");
            AddAlias("iwr", "Invoke-WebRequest");
            AddAlias("md", "mkdir");
            AddAlias("measure", "Measure-Object");
            AddAlias("mi", "Move-Item");
            AddAlias("move", "Move-Item");
            AddAlias("mp", "Move-ItemProperty");
            AddAlias("nal", "New-Alias");
            AddAlias("ndr", "New-PSDrive");
            AddAlias("ni", "New-Item");
            AddAlias("nmo", "New-Module");
            AddAlias("nsn", "New-PSSession");
            AddAlias("nv", "New-Variable");
            AddAlias("oh", "Out-Host");
            AddAlias("popd", "Pop-Location");
            AddAlias("pushd", "Push-Location");
            AddAlias("pwd", "Get-Location");
            AddAlias("r", "Invoke-History");
            AddAlias("rbp", "Remove-PSBreakpoint");
            AddAlias("rcjb", "Receive-Job");
            AddAlias("rd", "Remove-Item");
            AddAlias("rdr", "Remove-PSDrive");
            AddAlias("ren", "Rename-Item");
            AddAlias("ri", "Remove-Item");
            AddAlias("rjb", "Remove-Job");
            AddAlias("rmo", "Remove-Module");
            AddAlias("rni", "Rename-Item");
            AddAlias("rnp", "Rename-ItemProperty");
            AddAlias("rp", "Remove-ItemProperty");
            AddAlias("rsn", "Remove-PSSession");
            AddAlias("rv", "Remove-Variable");
            AddAlias("rvpa", "Resolve-Path");
            AddAlias("sajb", "Start-Job");
            AddAlias("sal", "Set-Alias");
            AddAlias("saps", "Start-Process");
            AddAlias("sbp", "Set-PSBreakpoint");
            AddAlias("scb", "Set-Clipboard");
            AddAlias("select", "Select-Object");
            AddAlias("set", "Set-Variable");
            AddAlias("si", "Set-Item");
            AddAlias("sl", "Set-Location");
            AddAlias("sls", "Select-String");
            AddAlias("sp", "Set-ItemProperty");
            AddAlias("spjb", "Stop-Job");
            AddAlias("spps", "Stop-Process");
            AddAlias("sv", "Set-Variable");
            AddAlias("type", "Get-Content");
            AddAlias("where", "Where-Object");
            AddAlias("wjb", "Wait-Job");
        }
    }
}
