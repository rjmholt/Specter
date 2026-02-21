// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Tools;
using System.Linq;
using Microsoft.PowerShell.ScriptAnalyzer.Configuration;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    /// <summary>
    /// AvoidUsingCmdletAliases: Check if cmdlet alias is used.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingCmdletAliases", typeof(Strings), nameof(Strings.AvoidUsingCmdletAliasesDescription))]
    public class AvoidUsingCmdletAliases : ScriptRule, IConfigurableRule<AvoidUsingCmdletAliasesConfiguration>
    {
        private static readonly IReadOnlyDictionary<string, string> s_knownAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "?", "Where-Object" },
            { "%", "ForEach-Object" },
            { "cd", "Set-Location" },
            { "chdir", "Set-Location" },
            { "clc", "Clear-Content" },
            { "clhy", "Clear-History" },
            { "cli", "Clear-Item" },
            { "clp", "Clear-ItemProperty" },
            { "cls", "Clear-Host" },
            { "clv", "Clear-Variable" },
            { "copy", "Copy-Item" },
            { "cpi", "Copy-Item" },
            { "cvpa", "Convert-Path" },
            { "dbp", "Disable-PSBreakpoint" },
            { "del", "Remove-Item" },
            { "dir", "Get-ChildItem" },
            { "ebp", "Enable-PSBreakpoint" },
            { "echo", "Write-Output" },
            { "epal", "Export-Alias" },
            { "epcsv", "Export-Csv" },
            { "erase", "Remove-Item" },
            { "etsn", "Enter-PSSession" },
            { "exsn", "Exit-PSSession" },
            { "fc", "Format-Custom" },
            { "fhx", "Format-Hex" },
            { "fl", "Format-List" },
            { "foreach", "ForEach-Object" },
            { "ft", "Format-Table" },
            { "fw", "Format-Wide" },
            { "gal", "Get-Alias" },
            { "gbp", "Get-PSBreakpoint" },
            { "gc", "Get-Content" },
            { "gcb", "Get-Clipboard" },
            { "gci", "Get-ChildItem" },
            { "gcm", "Get-Command" },
            { "gcs", "Get-PSCallStack" },
            { "gdr", "Get-PSDrive" },
            { "gerr", "Get-Error" },
            { "ghy", "Get-History" },
            { "gi", "Get-Item" },
            { "gjb", "Get-Job" },
            { "gl", "Get-Location" },
            { "gm", "Get-Member" },
            { "gmo", "Get-Module" },
            { "gp", "Get-ItemProperty" },
            { "gps", "Get-Process" },
            { "gpv", "Get-ItemPropertyValue" },
            { "group", "Group-Object" },
            { "gsn", "Get-PSSession" },
            { "gtz", "Get-TimeZone" },
            { "gu", "Get-Unique" },
            { "gv", "Get-Variable" },
            { "h", "Get-History" },
            { "history", "Get-History" },
            { "icm", "Invoke-Command" },
            { "iex", "Invoke-Expression" },
            { "ihy", "Invoke-History" },
            { "ii", "Invoke-Item" },
            { "ipal", "Import-Alias" },
            { "ipcsv", "Import-Csv" },
            { "ipmo", "Import-Module" },
            { "irm", "Invoke-RestMethod" },
            { "iwr", "Invoke-WebRequest" },
            { "md", "mkdir" },
            { "measure", "Measure-Object" },
            { "mi", "Move-Item" },
            { "move", "Move-Item" },
            { "mp", "Move-ItemProperty" },
            { "nal", "New-Alias" },
            { "ndr", "New-PSDrive" },
            { "ni", "New-Item" },
            { "nmo", "New-Module" },
            { "nsn", "New-PSSession" },
            { "nv", "New-Variable" },
            { "oh", "Out-Host" },
            { "popd", "Pop-Location" },
            { "pushd", "Push-Location" },
            { "pwd", "Get-Location" },
            { "r", "Invoke-History" },
            { "rbp", "Remove-PSBreakpoint" },
            { "rcjb", "Receive-Job" },
            { "rcsn", "" },
            { "rd", "Remove-Item" },
            { "rdr", "Remove-PSDrive" },
            { "ren", "Rename-Item" },
            { "ri", "Remove-Item" },
            { "rjb", "Remove-Job" },
            { "rmo", "Remove-Module" },
            { "rni", "Rename-Item" },
            { "rnp", "Rename-ItemProperty" },
            { "rp", "Remove-ItemProperty" },
            { "rsn", "Remove-PSSession" },
            { "rv", "Remove-Variable" },
            { "rvpa", "Resolve-Path" },
            { "sajb", "Start-Job" },
            { "sal", "Set-Alias" },
            { "saps", "Start-Process" },
            { "sbp", "Set-PSBreakpoint" },
            { "scb", "Set-Clipboard" },
            { "select", "Select-Object" },
            { "set", "Set-Variable" },
            { "si", "Set-Item" },
            { "sl", "Set-Location" },
            { "sls", "Select-String" },
            { "sp", "Set-ItemProperty" },
            { "spjb", "Stop-Job" },
            { "spps", "Stop-Process" },
            { "sv", "Set-Variable" },
            { "type", "Get-Content" },
            { "where", "Where-Object" },
            { "wjb", "Wait-Job" },
        };

        private static readonly IReadOnlyDictionary<string, string> s_knownGetAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Alias", "Get-Alias" },
            { "ChildItem", "Get-ChildItem" },
            { "Clipboard", "Get-Clipboard" },
            { "CmsMessage", "Get-CmsMessage" },
            { "Command", "Get-Command" },
            { "Content", "Get-Content" },
            { "Credential", "Get-Credential" },
            { "Culture", "Get-Culture" },
            { "Date", "Get-Date" },
            { "Error", "Get-Error" },
            { "Event", "Get-Event" },
            { "EventSubscriber", "Get-EventSubscriber" },
            { "ExecutionPolicy", "Get-ExecutionPolicy" },
            { "ExperimentalFeature", "Get-ExperimentalFeature" },
            { "FileHash", "Get-FileHash" },
            { "FormatData", "Get-FormatData" },
            { "Help", "Get-Help" },
            { "History", "Get-History" },
            { "Host", "Get-Host" },
            { "InstalledPSResource", "Get-InstalledPSResource" },
            { "Item", "Get-Item" },
            { "ItemProperty", "Get-ItemProperty" },
            { "ItemPropertyValue", "Get-ItemPropertyValue" },
            { "Job", "Get-Job" },
            { "Location", "Get-Location" },
            { "MarkdownOption", "Get-MarkdownOption" },
            { "Member", "Get-Member" },
            { "Module", "Get-Module" },
            { "Package", "Get-Package" },
            { "PackageProvider", "Get-PackageProvider" },
            { "PackageSource", "Get-PackageSource" },
            { "PfxCertificate", "Get-PfxCertificate" },
            { "Process", "Get-Process" },
            { "PSBreakpoint", "Get-PSBreakpoint" },
            { "PSCallStack", "Get-PSCallStack" },
            { "PSDrive", "Get-PSDrive" },
            { "PSHostProcessInfo", "Get-PSHostProcessInfo" },
            { "PSProvider", "Get-PSProvider" },
            { "PSReadLineKeyHandler", "Get-PSReadLineKeyHandler" },
            { "PSReadLineOption", "Get-PSReadLineOption" },
            { "PSResourceRepository", "Get-PSResourceRepository" },
            { "PSScriptFileInfo", "Get-PSScriptFileInfo" },
            { "PSSession", "Get-PSSession" },
            { "Random", "Get-Random" },
            { "Runspace", "Get-Runspace" },
            { "RunspaceDebug", "Get-RunspaceDebug" },
            { "SecureRandom", "Get-SecureRandom" },
            { "TimeZone", "Get-TimeZone" },
            { "TraceSource", "Get-TraceSource" },
            { "TypeData", "Get-TypeData" },
            { "UICulture", "Get-UICulture" },
            { "Unique", "Get-Unique" },
            { "Uptime", "Get-Uptime" },
            { "Variable", "Get-Variable" },
            { "Verb", "Get-Verb" },
        };

        public AvoidUsingCmdletAliases(RuleInfo ruleInfo, AvoidUsingCmdletAliasesConfiguration configuration) : base(ruleInfo)
        {
            Configuration = configuration;
        }

        public AvoidUsingCmdletAliasesConfiguration Configuration { get; }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that cmdlet aliases are not used.
        /// </summary>
        /// <param name="ast">The script's ast</param>
        /// <param name="tokens">The script's tokens</param>
        /// <param name="fileName">The script's file name</param>
        /// <returns>A List of diagnostic results of this rule</returns>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            IEnumerable<Ast> commandAsts = ast.FindAll(testAst => testAst is CommandAst, true);

            if (commandAsts == null)
            {
                yield break;
            }

            foreach (CommandAst cmdAst in commandAsts)
            {
                string commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                // Skip if command is in allowlist
                if (Configuration.AllowList.Contains(commandName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if command is an alias
                if (s_knownAliases.TryGetValue(commandName, out string cmdletNameIfCommandNameWasAlias))
                {
                    var correction = new AstCorrection(
                        cmdAst.CommandElements[0],
                        cmdletNameIfCommandNameWasAlias,
                        string.Format(CultureInfo.CurrentCulture, "Replace {0} with {1}", commandName, cmdletNameIfCommandNameWasAlias));

                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingCmdletAliasesError, commandName, cmdletNameIfCommandNameWasAlias),
                        cmdAst.CommandElements[0],
                        new[] { correction });
                    continue;
                }

                // Check for implicit Get- prefix aliases
                if (s_knownGetAliases.TryGetValue(commandName, out string getCommandName))
                {
                    var correction = new AstCorrection(
                        cmdAst.CommandElements[0],
                        getCommandName,
                        string.Format(CultureInfo.CurrentCulture, "{0} is an implicit get alias for {1}", commandName, getCommandName));

                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingCmdletAliasesMissingGetPrefixError, commandName, getCommandName),
                        cmdAst.CommandElements[0]);
                }
            }
        }
    }

    public record AvoidUsingCmdletAliasesConfiguration : IRuleConfiguration
    {
        public IReadOnlyCollection<string> AllowList { get; init; } = Array.Empty<string>();

        public CommonConfiguration Common { get; init; } = CommonConfiguration.Default;
    }
}