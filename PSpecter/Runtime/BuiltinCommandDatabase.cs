// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace PSpecter.Runtime
{
    /// <summary>
    /// A static command database populated from hardcoded PowerShell alias data.
    /// Does not require a PowerShell runspace or any runtime execution.
    /// This can be replaced with a database-backed implementation later.
    /// </summary>
    public class BuiltinCommandDatabase : IPowerShellCommandDatabase
    {
        public static BuiltinCommandDatabase Instance { get; } = new BuiltinCommandDatabase();

        private readonly Dictionary<string, string> _aliasToCommand;
        private readonly Dictionary<string, List<string>> _commandToAliases;

        private BuiltinCommandDatabase()
        {
            _aliasToCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _commandToAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            PopulateDefaultAliases();
        }

        private void AddAlias(string alias, string command)
        {
            _aliasToCommand[alias] = command;

            if (!_commandToAliases.TryGetValue(command, out List<string> aliases))
            {
                aliases = new List<string>();
                _commandToAliases[command] = aliases;
            }

            aliases.Add(alias);
        }

        public string GetAliasTarget(string alias)
        {
            return _aliasToCommand.TryGetValue(alias, out string target) ? target : null;
        }

        public IReadOnlyList<string> GetCommandAliases(string command)
        {
            return _commandToAliases.TryGetValue(command, out List<string> aliases) ? aliases : null;
        }

        public IReadOnlyList<string> GetAllNamesForCommand(string command)
        {
            var names = new List<string> { command };

            if (_commandToAliases.TryGetValue(command, out List<string> aliases))
            {
                names.AddRange(aliases);
            }

            return names;
        }

        /// <summary>
        /// Default PowerShell aliases shipped with the engine.
        /// This is the single source of truth for alias data used by rules.
        /// </summary>
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
