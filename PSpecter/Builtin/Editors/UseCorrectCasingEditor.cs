using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.CommandDatabase;
using PSpecter.Configuration;
using PSpecter.Formatting;

namespace PSpecter.Builtin.Editors
{
    public sealed class UseCorrectCasingEditorConfiguration : IEditorConfiguration
    {
        public CommonEditorConfiguration Common { get; set; } = new CommonEditorConfiguration();
        CommonConfiguration IRuleConfiguration.Common => new CommonConfiguration(Common.Enabled);
        public bool CheckKeyword { get; set; } = true;
        public bool CheckOperator { get; set; } = true;
    }

    [Editor("UseCorrectCasing", Description = "Normalizes casing for PowerShell keywords, operators, commands, and parameters")]
    public sealed class UseCorrectCasingEditor : IScriptEditor, IConfigurableEditor<UseCorrectCasingEditorConfiguration>
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        public UseCorrectCasingEditor(
            UseCorrectCasingEditorConfiguration configuration,
            IPowerShellCommandDatabase commandDb)
        {
            Configuration = configuration ?? new UseCorrectCasingEditorConfiguration();
            _commandDb = commandDb;
        }

        public UseCorrectCasingEditor(UseCorrectCasingEditorConfiguration configuration)
            : this(configuration, null)
        {
        }

        public UseCorrectCasingEditorConfiguration Configuration { get; }

        public IReadOnlyList<ScriptEdit> GetEdits(
            string scriptContent,
            Ast ast,
            IReadOnlyList<Token> tokens,
            string filePath)
        {
            if (scriptContent is null) { throw new ArgumentNullException(nameof(scriptContent)); }

            var edits = new List<ScriptEdit>();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (Configuration.CheckKeyword && (token.TokenFlags & TokenFlags.Keyword) != 0)
                {
                    TryAddLowercaseEdit(token, edits);
                }
                else if (Configuration.CheckOperator && IsOperator(token))
                {
                    TryAddLowercaseEdit(token, edits);
                }
            }

            if (_commandDb is not null && ast is not null)
            {
                AddCommandCasingEdits(ast, edits);
            }

            return edits;
        }

        private void AddCommandCasingEdits(Ast ast, List<ScriptEdit> edits)
        {
            foreach (Ast node in ast.FindAll(a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;
                string commandName = cmdAst.GetCommandName();
                if (commandName is null)
                {
                    continue;
                }

                // Skip file paths and script invocations, but NOT module-qualified
                // names like "Module\Cmdlet" (single backslash without path separators)
                if (IsFilePath(commandName))
                {
                    continue;
                }

                CommandElementAst nameElement = cmdAst.CommandElements[0];

                string canonicalName = ResolveCanonicalName(commandName);
                if (canonicalName is not null && canonicalName != nameElement.Extent.Text)
                {
                    string replacement = canonicalName;
                    // For module-qualified names like "Module\Cmdlet"
                    if (nameElement.Extent.Text.Contains("\\"))
                    {
                        int slashIndex = nameElement.Extent.Text.IndexOf('\\');
                        string modulePrefix = nameElement.Extent.Text.Substring(0, slashIndex + 1);
                        replacement = modulePrefix + canonicalName;

                        // Also try to correct the module prefix casing
                        if (_commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata cmd)
                            && cmd.ModuleName is not null)
                        {
                            string originalModule = nameElement.Extent.Text.Substring(0, slashIndex);
                            if (!string.Equals(originalModule, cmd.ModuleName, StringComparison.Ordinal)
                                && string.Equals(originalModule, cmd.ModuleName, StringComparison.OrdinalIgnoreCase))
                            {
                                replacement = cmd.ModuleName + "\\" + canonicalName;
                            }
                        }
                    }

                    edits.Add(new ScriptEdit(
                        nameElement.Extent.StartOffset,
                        nameElement.Extent.EndOffset,
                        replacement));
                }

                AddParameterCasingEdits(cmdAst, commandName, edits);
            }
        }

        private void AddParameterCasingEdits(CommandAst cmdAst, string commandName, List<ScriptEdit> edits)
        {
            if (!_commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata cmd))
            {
                return;
            }

            if (cmd.Parameters is null || cmd.Parameters.Count == 0)
            {
                return;
            }

            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is not CommandParameterAst paramAst)
                {
                    continue;
                }

                string paramName = paramAst.ParameterName;
                string canonical = FindCanonicalParameterName(cmd, paramName);
                if (canonical is not null && canonical != paramName)
                {
                    int dashOffset = paramAst.Extent.StartOffset;
                    int nameEnd = dashOffset + 1 + paramName.Length;
                    edits.Add(new ScriptEdit(
                        dashOffset + 1,
                        nameEnd,
                        canonical));
                }
            }
        }

        private string ResolveCanonicalName(string commandName)
        {
            // First check if it's an alias, so we preserve the alias form with canonical casing
            string aliasTarget = _commandDb.GetAliasTarget(commandName);
            if (aliasTarget is not null)
            {
                IReadOnlyList<string> aliases = _commandDb.GetCommandAliases(aliasTarget);
                if (aliases is not null)
                {
                    foreach (string alias in aliases)
                    {
                        if (string.Equals(alias, commandName, StringComparison.OrdinalIgnoreCase))
                        {
                            return alias;
                        }
                    }
                }
                return commandName;
            }

            // Check if it's a canonical command name
            if (_commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata cmd))
            {
                return cmd.Name;
            }

            return null;
        }

        private static string FindCanonicalParameterName(CommandMetadata cmd, string paramName)
        {
            foreach (var param in cmd.Parameters)
            {
                if (string.Equals(param.Name, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return param.Name;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a command name looks like a file path rather than a
        /// cmdlet or module-qualified cmdlet name. Module-qualified names like
        /// "Microsoft.PowerShell.Management\Get-ChildItem" use a single backslash
        /// with no forward slashes or file extensions.
        /// </summary>
        private static bool IsFilePath(string commandName)
        {
            if (commandName.Contains("/"))
            {
                return true;
            }

            if (commandName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                || commandName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || commandName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || commandName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // A single backslash is a module-qualified name, not a file path.
            // Multiple backslashes or backslash combined with other path indicators
            // suggest a real file path.
            int backslashCount = 0;
            foreach (char c in commandName)
            {
                if (c == '\\')
                {
                    backslashCount++;
                    if (backslashCount > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void TryAddLowercaseEdit(Token token, List<ScriptEdit> edits)
        {
            string text = token.Text;
            string lower = text.ToLowerInvariant();

            if (text != lower)
            {
                edits.Add(new ScriptEdit(
                    token.Extent.StartOffset,
                    token.Extent.EndOffset,
                    lower));
            }
        }

        private static bool IsOperator(Token token)
        {
            return (token.TokenFlags & TokenFlags.BinaryOperator) != 0
                || (token.TokenFlags & TokenFlags.UnaryOperator) != 0;
        }
    }
}
