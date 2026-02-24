using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    /// <summary>
    /// Flags variables that are read but never assigned in the current scope.
    /// PowerShell's dynamic scoping means unassigned variables silently inherit
    /// values from the caller, which is a common source of bugs.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingUninitializedVariable", typeof(Strings), nameof(Strings.AvoidUsingUninitializedVariableDescription))]
    internal class AvoidUsingUninitializedVariable : ScriptRule
    {
        internal AvoidUsingUninitializedVariable(RuleInfo ruleInfo)
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

        internal IEnumerable<ScriptDiagnostic> AnalyzeScriptBlock(ScriptBlockAst scriptBlock)
        {
            return AnalyzeScriptBlock(scriptBlock, assignedFromOuter: null);
        }

        /// <param name="assignedFromOuter">
        /// Optional set of variable names already known to be assigned (e.g. from
        /// branch-aware analysis). When non-null, these names are treated as assigned
        /// in addition to whatever the scope itself assigns.
        /// </param>
        internal IEnumerable<ScriptDiagnostic> AnalyzeScriptBlock(
            ScriptBlockAst scriptBlock,
            IEnumerable<string>? assignedFromOuter)
        {
            var assignedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (assignedFromOuter is not null)
            {
                foreach (string name in assignedFromOuter)
                {
                    assignedVars.Add(name);
                }
            }

            CollectParameters(scriptBlock, assignedVars);
            CollectAssignments(scriptBlock, assignedVars);

            var alreadyReported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Ast found in scriptBlock.FindAll(static a => a is VariableExpressionAst, searchNestedScriptBlocks: false))
            {
                var varAst = (VariableExpressionAst)found;

                if (ShouldSkipVariable(varAst) || IsAssignmentTarget(varAst))
                {
                    continue;
                }

                string varName = varAst.GetNameWithoutScope();

                if (!assignedVars.Contains(varName) && alreadyReported.Add(varName))
                {
                    yield return CreateDiagnostic(
                        string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingUninitializedVariableError, varName),
                        varAst.Extent);
                }
            }
        }

        private static void CollectParameters(ScriptBlockAst scriptBlock, HashSet<string> assignedVars)
        {
            if (scriptBlock.ParamBlock?.Parameters is not null)
            {
                foreach (ParameterAst param in scriptBlock.ParamBlock.Parameters)
                {
                    assignedVars.Add(param.Name.GetNameWithoutScope());
                }
            }

            if (scriptBlock.Parent is FunctionDefinitionAst funcDef && funcDef.Parameters is not null)
            {
                foreach (ParameterAst param in funcDef.Parameters)
                {
                    assignedVars.Add(param.Name.GetNameWithoutScope());
                }
            }
        }

        private static void CollectAssignments(ScriptBlockAst scriptBlock, HashSet<string> assignedVars)
        {
            foreach (Ast found in scriptBlock.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)found;
                VariableExpressionAst? varAst = ExtractAssignmentVariable(assignment);

                if (varAst is not null && !IsScopeQualified(varAst))
                {
                    assignedVars.Add(varAst.GetNameWithoutScope());
                }
            }

            foreach (Ast found in scriptBlock.FindAll(static a => a is ForEachStatementAst, searchNestedScriptBlocks: false))
            {
                var foreachAst = (ForEachStatementAst)found;
                assignedVars.Add(foreachAst.Variable.GetNameWithoutScope());
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

        private static bool ShouldSkipVariable(VariableExpressionAst varAst)
        {
            return IsScopeQualified(varAst)
                || varAst.IsSpecialVariable()
                || IsInsideUsing(varAst);
        }

        private static bool IsScopeQualified(VariableExpressionAst varAst)
        {
            return varAst.VariablePath.IsGlobal
                || varAst.VariablePath.IsScript
                || varAst.VariablePath.DriveName is not null;
        }

        private static bool IsInsideUsing(VariableExpressionAst varAst)
        {
            Ast? parent = varAst.Parent;
            while (parent is not null)
            {
                if (parent is UsingExpressionAst)
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static bool IsAssignmentTarget(VariableExpressionAst varAst)
        {
            Ast? parent = varAst.Parent;

            if (parent is AssignmentStatementAst assignment)
            {
                return ReferenceEquals(assignment.Left, varAst);
            }

            if (parent is ConvertExpressionAst convert
                && convert.Parent is AssignmentStatementAst outerAssignment)
            {
                return ReferenceEquals(outerAssignment.Left, convert);
            }

            return false;
        }
    }
}
