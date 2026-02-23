using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("PreferTypeConstructor", typeof(Strings), nameof(Strings.PreferTypeConstructorDescription), Severity = DiagnosticSeverity.Information)]
    internal class PreferTypeConstructor : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal PreferTypeConstructor(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)found;
                string? commandName = cmdAst.GetCommandName();

                if (commandName is null || !_commandDb.IsCommandOrAlias(commandName, "New-Object"))
                {
                    continue;
                }

                if (HasComObjectParameter(cmdAst))
                {
                    continue;
                }

                string? typeName = GetTypeNameArgument(cmdAst);
                if (typeName is not null)
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.PreferTypeConstructorError, typeName),
                        cmdAst.Extent);
                }
            }
        }

        private static bool HasComObjectParameter(CommandAst cmdAst)
        {
            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is CommandParameterAst param
                    && param.ParameterName.StartsWith("Com", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? GetTypeNameArgument(CommandAst cmdAst)
        {
            var elements = cmdAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is CommandParameterAst param
                    && param.ParameterName.StartsWith("TypeN", StringComparison.OrdinalIgnoreCase))
                {
                    ExpressionAst? valueAst = param.Argument;
                    if (valueAst is null && i + 1 < elements.Count)
                    {
                        valueAst = elements[i + 1] as ExpressionAst;
                    }

                    if (valueAst is StringConstantExpressionAst constant)
                    {
                        return constant.Value;
                    }

                    return null;
                }

                // Positional: first non-parameter argument is the type name
                if (elements[i] is StringConstantExpressionAst positional && i == 1)
                {
                    return positional.Value;
                }
            }

            return null;
        }
    }
}
