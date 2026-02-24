using System;
using System.Collections.Generic;
using Specter.CommandDatabase;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    /// <summary>
    /// AvoidGlobalAliases: Check that New-Alias is not called with -Scope Global in module scripts.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidGlobalAliases", typeof(Strings), nameof(Strings.AvoidGlobalAliasesDescription))]
    internal class AvoidGlobalAliases : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDatabase;

        internal AvoidGlobalAliases(
            RuleInfo ruleInfo,
            IPowerShellCommandDatabase commandDatabase)
            : base(ruleInfo)
        {
            _commandDatabase = commandDatabase;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (!AstExtensions.IsModuleScript(scriptPath))
            {
                yield break;
            }

            IEnumerable<Ast> commandAsts = ast.FindAll(static testAst => testAst is CommandAst, true);

            foreach (CommandAst commandAst in commandAsts)
            {
                string commandName = commandAst.GetCommandName();

                if (commandName == null)
                {
                    continue;
                }

                if (!string.Equals(commandName, "New-Alias", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(commandName, "nal", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _commandDatabase.TryGetCommand(commandName, platforms: null, out CommandMetadata? commandMetadata);
                if (!CommandAstInspector.TryGetBoundParameterConstantValue(
                    commandAst,
                    commandMetadata,
                    parameterName: "Scope",
                    out object? scopeValue))
                {
                    continue;
                }

                if (scopeValue is not null
                    && string.Equals(scopeValue.ToString(), "Global", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateDiagnostic(Strings.AvoidGlobalAliasesError, commandAst);
                }
            }
        }
    }
}
