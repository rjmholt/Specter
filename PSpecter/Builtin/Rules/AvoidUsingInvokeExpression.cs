using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.CommandDatabase;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingInvokeExpression", typeof(Strings), nameof(Strings.AvoidUsingInvokeExpressionRuleDescription))]
    internal class AvoidUsingInvokeExpression : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidUsingInvokeExpression(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
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

            foreach (Ast foundAst in ast.FindAll(static testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string commandName = cmdAst.GetCommandName();

                if (commandName is null || !_commandDb.IsCommandOrAlias(commandName, "Invoke-Expression"))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingInvokeExpressionError),
                    cmdAst.Extent);
            }
        }
    }
}
