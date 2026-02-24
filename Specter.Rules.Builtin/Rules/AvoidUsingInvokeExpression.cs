using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Rules;
using Specter.CommandDatabase;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    internal class AvoidUsingInvokeExpressionConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enable: true);

        public bool AllowConstantArguments { get; set; }
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingInvokeExpression", typeof(Strings), nameof(Strings.AvoidUsingInvokeExpressionRuleDescription))]
    internal class AvoidUsingInvokeExpression : ConfigurableScriptRule<AvoidUsingInvokeExpressionConfiguration>
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidUsingInvokeExpression(RuleInfo ruleInfo, AvoidUsingInvokeExpressionConfiguration configuration, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo, configuration)
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

                if (Configuration.AllowConstantArguments && HasOnlyConstantArguments(cmdAst))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingInvokeExpressionError),
                    cmdAst.Extent);
            }
        }

        private static bool HasOnlyConstantArguments(CommandAst cmdAst)
        {
            var elements = cmdAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is CommandParameterAst)
                {
                    continue;
                }

                if (elements[i] is StringConstantExpressionAst)
                {
                    continue;
                }

                if (elements[i] is ConstantExpressionAst && elements[i] is not ExpandableStringExpressionAst)
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
