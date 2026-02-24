using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidForeachObjectInjection", typeof(Strings), nameof(Strings.AvoidForeachObjectInjectionDescription))]
    internal class AvoidForeachObjectInjection : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidForeachObjectInjection(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
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

                if (commandName is null || !_commandDb.IsCommandOrAlias(commandName, "ForEach-Object"))
                {
                    continue;
                }

                if (TryFindUnsafeMemberName(cmdAst, out IScriptExtent? flagExtent))
                {
                    yield return CreateDiagnostic(
                        Strings.AvoidForeachObjectInjectionError,
                        flagExtent!);
                }
            }
        }

        private static bool TryFindUnsafeMemberName(CommandAst commandAst, out IScriptExtent? extent)
        {
            var elements = commandAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is not CommandParameterAst param)
                {
                    continue;
                }

                if (!param.ParameterName.StartsWith("MemberN", StringComparison.OrdinalIgnoreCase))
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

                if (valueAst is StringConstantExpressionAst)
                {
                    continue;
                }

                extent = valueAst.Extent;
                return true;
            }

            extent = null;
            return false;
        }
    }
}
