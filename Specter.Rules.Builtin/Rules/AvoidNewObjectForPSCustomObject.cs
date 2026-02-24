using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidNewObjectForPSCustomObject", typeof(Strings), nameof(Strings.AvoidNewObjectForPSCustomObjectDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidNewObjectForPSCustomObject : ScriptRule
    {
        private static readonly HashSet<string> s_customObjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PSObject",
            "PSCustomObject",
            "System.Management.Automation.PSObject",
            "System.Management.Automation.PSCustomObject",
        };

        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidNewObjectForPSCustomObject(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
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

                if (IsPSCustomObjectWithProperty(cmdAst))
                {
                    yield return CreateDiagnostic(
                        Strings.AvoidNewObjectForPSCustomObjectError,
                        cmdAst.Extent);
                }
            }
        }

        private static bool IsPSCustomObjectWithProperty(CommandAst cmdAst)
        {
            bool isPSObjectType = false;
            bool hasProperty = false;

            var elements = cmdAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is CommandParameterAst param)
                {
                    if (param.ParameterName.StartsWith("TypeN", StringComparison.OrdinalIgnoreCase))
                    {
                        ExpressionAst? valueAst = param.Argument;
                        if (valueAst is null && i + 1 < elements.Count)
                        {
                            valueAst = elements[i + 1] as ExpressionAst;
                        }

                        if (valueAst is StringConstantExpressionAst constant
                            && s_customObjectTypes.Contains(constant.Value))
                        {
                            isPSObjectType = true;
                        }
                    }
                    else if (param.ParameterName.StartsWith("Prop", StringComparison.OrdinalIgnoreCase))
                    {
                        hasProperty = true;
                    }

                    continue;
                }

                // Positional type name (first non-parameter argument)
                if (i == 1 && elements[i] is StringConstantExpressionAst positional
                    && s_customObjectTypes.Contains(positional.Value))
                {
                    isPSObjectType = true;
                }
            }

            return isPSObjectType && hasProperty;
        }
    }
}
