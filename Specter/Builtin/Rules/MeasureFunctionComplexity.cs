using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    internal class MeasureFunctionComplexityConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enable: true);

        public int MaxComplexity { get; set; } = 25;
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("MeasureFunctionComplexity", typeof(Strings), nameof(Strings.MeasureFunctionComplexityDescription))]
    internal class MeasureFunctionComplexity : ConfigurableScriptRule<MeasureFunctionComplexityConfiguration>
    {
        internal MeasureFunctionComplexity(RuleInfo ruleInfo, MeasureFunctionComplexityConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            int threshold = Configuration.MaxComplexity;

            foreach (Ast found in ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)found;
                int complexity = CalculateComplexity(funcAst.Body);

                if (complexity > threshold)
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.MeasureFunctionComplexityError,
                            funcAst.Name,
                            complexity,
                            threshold),
                        funcAst.Extent);
                }
            }
        }

        private static int CalculateComplexity(ScriptBlockAst scriptBlock)
        {
            int complexity = 1;

            foreach (Ast node in scriptBlock.FindAll(IsComplexityNode, searchNestedScriptBlocks: true))
            {
                if (IsInsideNestedFunction(node, scriptBlock))
                {
                    continue;
                }

                if (node is IfStatementAst ifAst)
                {
                    complexity += ifAst.Clauses.Count;
                }
                else if (node is SwitchStatementAst switchAst)
                {
                    complexity += switchAst.Clauses.Count;
                }
                else
                {
                    complexity++;
                }
            }

            return complexity;
        }

        private static bool IsComplexityNode(Ast a)
        {
            return a is IfStatementAst
                || a is SwitchStatementAst
                || a is WhileStatementAst
                || a is DoWhileStatementAst
                || a is DoUntilStatementAst
                || a is ForStatementAst
                || a is ForEachStatementAst
                || a is CatchClauseAst
                || a is TrapStatementAst
                || (a is BinaryExpressionAst b && (b.Operator == TokenKind.And || b.Operator == TokenKind.Or));
        }

        private static bool IsInsideNestedFunction(Ast node, ScriptBlockAst outerScope)
        {
            Ast? current = node.Parent;
            while (current is not null && current != outerScope)
            {
                if (current is FunctionDefinitionAst)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
