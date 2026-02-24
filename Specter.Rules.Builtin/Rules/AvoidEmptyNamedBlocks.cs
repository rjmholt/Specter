using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidEmptyNamedBlocks", typeof(Strings), nameof(Strings.AvoidEmptyNamedBlocksDescription))]
    internal class AvoidEmptyNamedBlocks : ScriptRule
    {
        internal AvoidEmptyNamedBlocks(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)found;
                ScriptBlockAst body = funcAst.Body;

                if (IsEmptyNamedBlock(body.BeginBlock))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidEmptyNamedBlocksError, "begin"),
                        body.BeginBlock!.Extent);
                }

                if (IsEmptyNamedBlock(body.ProcessBlock))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidEmptyNamedBlocksError, "process"),
                        body.ProcessBlock!.Extent);
                }

                if (IsEmptyNamedBlock(body.EndBlock))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidEmptyNamedBlocksError, "end"),
                        body.EndBlock!.Extent);
                }

                if (IsEmptyNamedBlock(body.DynamicParamBlock))
                {
                    yield return CreateDiagnostic(
                        string.Format(Strings.AvoidEmptyNamedBlocksError, "dynamicparam"),
                        body.DynamicParamBlock!.Extent);
                }
            }
        }

        private static bool IsEmptyNamedBlock(NamedBlockAst? block)
        {
            if (block is null || block.Unnamed)
            {
                return false;
            }

            return block.Statements.Count == 0
                && (block.Traps is null || block.Traps.Count == 0);
        }
    }
}
