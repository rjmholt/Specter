using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using PSpecter.Rules;
using PSpecter;
using PSpecter.Builtin.Rules;
using PSpecter.CommandDatabase;
using PSpecter.Tools;
using System.Linq;
using PSpecter.Configuration;

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
        private readonly IPowerShellCommandDatabase _commandDb;

        // Implicit Get- prefix completions are a separate concept from standard aliases.
        // PowerShell resolves bare nouns (e.g. "Variable") to their Get- prefixed form (e.g. "Get-Variable").
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

        public AvoidUsingCmdletAliases(RuleInfo ruleInfo, AvoidUsingCmdletAliasesConfiguration configuration, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo)
        {
            Configuration = configuration;
            _commandDb = commandDb;
        }

        public AvoidUsingCmdletAliasesConfiguration Configuration { get; }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that cmdlet aliases are not used.
        /// </summary>
        /// <param name="ast">The script's ast</param>
        /// <param name="tokens">The script's tokens</param>
        /// <param name="fileName">The script's file name</param>
        /// <returns>A List of diagnostic results of this rule</returns>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            IEnumerable<Ast> commandAsts = ast.FindAll(testAst => testAst is CommandAst, true);

            if (commandAsts is null)
            {
                yield break;
            }

            foreach (CommandAst cmdAst in commandAsts)
            {
                if (cmdAst.CommandElements.Count == 0)
                {
                    continue;
                }

                CommandElementAst firstElement = cmdAst.CommandElements[0];
                if (firstElement is null)
                {
                    continue;
                }

                string? commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                // Skip if command is in allowlist
                if (Configuration.AllowList.Contains(commandName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if command is a known alias
                string? cmdletNameIfCommandNameWasAlias = _commandDb.GetAliasTarget(commandName);
                if (cmdletNameIfCommandNameWasAlias is not null)
                {
                    var correction = new AstCorrection(
                        firstElement,
                        cmdletNameIfCommandNameWasAlias,
                        string.Format(CultureInfo.CurrentCulture, "Replace {0} with {1}", commandName, cmdletNameIfCommandNameWasAlias));

                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingCmdletAliasesError, commandName, cmdletNameIfCommandNameWasAlias),
                        firstElement,
                        new[] { correction });
                    continue;
                }

                // Check for implicit Get- prefix aliases, but skip if the command
                // exists as a real (non-alias) command (e.g. native 'date' on Unix)
                // Also skip named block keywords (begin/process/end) used with scriptblocks
                if (s_knownGetAliases.TryGetValue(commandName, out string? getCommandName)
                    && !IsNamedBlockKeywordUsage(cmdAst))
                {
                    if (_commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata? meta)
                        && meta is not null && !string.Equals(meta.CommandType, "Alias", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var correction = new AstCorrection(
                        firstElement,
                        getCommandName,
                        string.Format(CultureInfo.CurrentCulture, "{0} is an implicit get alias for {1}", commandName, getCommandName));

                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingCmdletAliasesMissingGetPrefixError, commandName, getCommandName),
                        firstElement);
                    continue;
                }

                // Detect misplaced statements before named blocks in functions.
                // e.g. function foo { SomeName; process {} } — the bare name
                // before a named block is almost certainly a mistake.
                if (IsStatementBeforeNamedBlock(cmdAst))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingCmdletAliasesError, commandName, commandName),
                        firstElement,
                        DiagnosticSeverity.ParseError);
                }
            }
        }

        private static readonly HashSet<string> s_namedBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "begin", "process", "end", "clean",
        };

        private static bool IsNamedBlockKeywordUsage(CommandAst cmdAst)
        {
            string? name = cmdAst.GetCommandName();
            return name is not null
                && s_namedBlockKeywords.Contains(name)
                && cmdAst.CommandElements.Count == 2
                && cmdAst.CommandElements[1] is ScriptBlockExpressionAst;
        }

        private static bool IsStatementBeforeNamedBlock(CommandAst cmdAst)
        {
            if (cmdAst.Parent is not PipelineAst pipeline)
            {
                return false;
            }

            if (pipeline.Parent is not NamedBlockAst namedBlock || !namedBlock.Unnamed)
            {
                return false;
            }

            if (namedBlock.Parent is not ScriptBlockAst scriptBlock)
            {
                return false;
            }

            if (scriptBlock.Parent is not FunctionDefinitionAst)
            {
                return false;
            }

            // Check if sibling statements include commands that look like
            // named blocks (e.g. "process {}"), which indicates the function
            // has a mix of bare statements and intended named blocks.
            foreach (StatementAst sibling in namedBlock.Statements)
            {
                if (sibling is PipelineAst siblingPipeline
                    && siblingPipeline.PipelineElements.Count == 1
                    && siblingPipeline.PipelineElements[0] is CommandAst siblingCmd
                    && !ReferenceEquals(siblingCmd, cmdAst))
                {
                    string? siblingName = siblingCmd.GetCommandName();
                    if (siblingName is not null
                        && s_namedBlockKeywords.Contains(siblingName)
                        && siblingCmd.CommandElements.Count == 2
                        && siblingCmd.CommandElements[1] is ScriptBlockExpressionAst)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public record AvoidUsingCmdletAliasesConfiguration : IRuleConfiguration
    {
        public IReadOnlyCollection<string> AllowList { get; init; } = Array.Empty<string>();

        public CommonConfiguration Common { get; init; } = CommonConfiguration.Default;
    }
}