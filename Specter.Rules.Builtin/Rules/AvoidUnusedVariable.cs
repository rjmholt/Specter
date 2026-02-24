using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUnusedVariable", typeof(Strings), nameof(Strings.AvoidUnusedVariableDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidUnusedVariable : ScriptRule
    {
        internal AvoidUnusedVariable(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is ScriptBlockAst, searchNestedScriptBlocks: true))
            {
                foreach (ScriptDiagnostic diag in AnalyzeScriptBlock((ScriptBlockAst)found))
                {
                    yield return diag;
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeScriptBlock(ScriptBlockAst scriptBlock)
        {
            var assignments = new Dictionary<string, AssignmentStatementAst>(StringComparer.OrdinalIgnoreCase);
            var readVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect local assignments (non-recursive into nested script blocks)
            foreach (Ast found in scriptBlock.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)found;
                VariableExpressionAst? varAst = ExtractAssignmentVariable(assignment);

                if (varAst is null)
                {
                    continue;
                }

                if (IsScopeQualified(varAst))
                {
                    continue;
                }

                string varName = varAst.GetNameWithoutScope();

                // $null = expr is intentional discard
                if (string.Equals(varName, "null", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!assignments.ContainsKey(varName))
                {
                    assignments[varName] = assignment;
                }
            }

            // Collect reads (include nested scopes — closures can capture enclosing variables)
            foreach (Ast found in scriptBlock.FindAll(static a => a is VariableExpressionAst, searchNestedScriptBlocks: true))
            {
                var varAst = (VariableExpressionAst)found;

                if (IsAssignmentTarget(varAst))
                {
                    continue;
                }

                readVars.Add(varAst.GetNameWithoutScope());
            }

            // Also count splatted usages (@params reads $params)
            foreach (Ast found in scriptBlock.FindAll(
                static a => a is VariableExpressionAst v && v.Splatted,
                searchNestedScriptBlocks: true))
            {
                var varAst = (VariableExpressionAst)found;
                readVars.Add(varAst.GetNameWithoutScope());
            }

            foreach (KeyValuePair<string, AssignmentStatementAst> entry in assignments)
            {
                if (SpecialVariables.IsSpecialVariable(entry.Key))
                {
                    continue;
                }

                if (!readVars.Contains(entry.Key))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUnusedVariableError, entry.Key),
                        entry.Value.Left.Extent);
                }
            }
        }

        private static VariableExpressionAst? ExtractAssignmentVariable(AssignmentStatementAst assignment)
        {
            if (assignment.Left is VariableExpressionAst varAst)
            {
                return varAst;
            }

            if (assignment.Left is ConvertExpressionAst convert)
            {
                return convert.Child as VariableExpressionAst;
            }

            return null;
        }

        private static bool IsScopeQualified(VariableExpressionAst varAst)
        {
            return varAst.VariablePath.IsGlobal
                || varAst.VariablePath.IsScript
                || varAst.VariablePath.DriveName is not null;
        }

        private static bool IsAssignmentTarget(VariableExpressionAst varAst)
        {
            Ast? parent = varAst.Parent;

            if (parent is AssignmentStatementAst assignment)
            {
                return IsDescendantOf(varAst, assignment.Left);
            }

            if (parent is ConvertExpressionAst convert
                && convert.Parent is AssignmentStatementAst outerAssignment)
            {
                return IsDescendantOf(varAst, outerAssignment.Left);
            }

            return false;
        }

        private static bool IsDescendantOf(Ast node, Ast ancestor)
        {
            Ast? current = node;
            while (current is not null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
