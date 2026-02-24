using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Configuration;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    internal class AvoidPositionalParametersConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enable: true);

        public string[] CommandAllowList { get; set; } = Array.Empty<string>();
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidUsingPositionalParameters", typeof(Strings), nameof(Strings.AvoidUsingPositionalParametersDescription),
        Severity = DiagnosticSeverity.Information)]
    internal class AvoidPositionalParameters : ConfigurableScriptRule<AvoidPositionalParametersConfiguration>
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidPositionalParameters(
            RuleInfo ruleInfo,
            AvoidPositionalParametersConfiguration configuration,
            IPowerShellCommandDatabase commandDb)
            : base(ruleInfo, configuration)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var allowList = new HashSet<string>(
                Configuration.CommandAllowList ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            ScriptModel scriptModel = ScriptModel.GetOrCreate(ast, tokens, scriptPath);

            foreach (Ast node in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;
                string commandName = cmdAst.GetCommandName();
                if (commandName == null)
                {
                    continue;
                }

                if (allowList.Contains(commandName))
                {
                    continue;
                }

                FunctionSymbol? functionSymbol = scriptModel.GetFunctionSymbol(commandName);
                if (functionSymbol is not null)
                {
                    if (functionSymbol.IsVariadic)
                    {
                        continue;
                    }
                }
                else if (!IsKnownCmdlet(commandName) && !LooksLikeCmdlet(commandName))
                {
                    continue;
                }

                if (IsApplicationCommand(commandName))
                {
                    continue;
                }

                if (HasNoParameters(commandName))
                {
                    continue;
                }

                if (HasSplattedVariable(cmdAst))
                {
                    continue;
                }

                if (!HasPositionalParameters(cmdAst, moreThanTwo: true))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AvoidUsingPositionalParametersError,
                        commandName),
                    cmdAst.Extent);
            }
        }

        private bool IsKnownCmdlet(string commandName)
        {
            return _commandDb.TryGetCommand(commandName, platforms: null, out _)
                || _commandDb.GetAliasTarget(commandName) != null
                || _commandDb.GetCommandAliases(commandName) != null;
        }

        private bool IsApplicationCommand(string commandName)
        {
            return _commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata? metadata)
                && string.Equals(metadata!.CommandType, "Application", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasNoParameters(string commandName)
        {
            return _commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata? metadata)
                && metadata!.Parameters.Count == 0;
        }

        private static bool LooksLikeCmdlet(string commandName)
        {
            int hyphen = commandName.IndexOf('-');
            return hyphen > 0 && hyphen < commandName.Length - 1;
        }

        private static bool HasSplattedVariable(CommandAst cmdAst)
        {
            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is VariableExpressionAst varExpr && varExpr.Splatted)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPositionalParameters(CommandAst cmdAst, bool moreThanTwo)
        {
            int positionalCount = 0;

            var elements = cmdAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (!(elements[i] is CommandParameterAst) && !(elements[i - 1] is CommandParameterAst))
                {
                    positionalCount++;
                }
            }

            var parent = cmdAst.Parent as PipelineAst;
            if (parent is not null && parent.PipelineElements.Count > 1 && parent.PipelineElements[0] != cmdAst)
            {
                positionalCount++;
            }

            return moreThanTwo ? positionalCount > 2 : positionalCount > 0;
        }
    }
}
