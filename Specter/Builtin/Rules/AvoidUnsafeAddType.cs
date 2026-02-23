using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUnsafeAddType", typeof(Strings), nameof(Strings.AvoidUnsafeAddTypeDescription))]
    internal class AvoidUnsafeAddType : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidUnsafeAddType(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
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

                if (commandName is null || !_commandDb.IsCommandOrAlias(commandName, "Add-Type"))
                {
                    continue;
                }

                if (TryGetUnsafeTypeDefinition(cmdAst, out IScriptExtent? flagExtent))
                {
                    yield return CreateDiagnostic(
                        Strings.AvoidUnsafeAddTypeError,
                        flagExtent!);
                }
            }
        }

        private static bool TryGetUnsafeTypeDefinition(CommandAst commandAst, out IScriptExtent? extent)
        {
            var elements = commandAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is not CommandParameterAst param)
                {
                    continue;
                }

                if (!param.ParameterName.StartsWith("TypeD", StringComparison.OrdinalIgnoreCase)
                    && !param.ParameterName.StartsWith("MemberD", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ExpressionAst? valueAst = param.Argument;
                if (valueAst is null && i + 1 < elements.Count)
                {
                    valueAst = elements[i + 1] as ExpressionAst;
                }

                if (valueAst is null)
                {
                    continue;
                }

                if (IsConstantExpression(valueAst))
                {
                    continue;
                }

                extent = valueAst.Extent;
                return true;
            }

            extent = null;
            return false;
        }

        private static bool IsConstantExpression(ExpressionAst expression)
        {
            return expression is StringConstantExpressionAst
                || (expression is ConstantExpressionAst && expression is not ExpandableStringExpressionAst);
        }
    }
}
