using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.Configuration;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    internal class AvoidVariableShadowingConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enable: true);

        public string[] ExcludeVariables { get; set; } = new[] { "_", "PSItem", "args" };
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidVariableShadowing", typeof(Strings), nameof(Strings.AvoidVariableShadowingDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidVariableShadowing : ConfigurableScriptRule<AvoidVariableShadowingConfiguration>
    {
        internal AvoidVariableShadowing(RuleInfo ruleInfo, AvoidVariableShadowingConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var excluded = new HashSet<string>(Configuration.ExcludeVariables, StringComparer.OrdinalIgnoreCase);

            foreach (Ast found in ast.FindAll(static a => a is ScriptBlockAst, searchNestedScriptBlocks: true))
            {
                var scriptBlock = (ScriptBlockAst)found;
                var outerVars = CollectOuterScopeVariables(scriptBlock);

                if (outerVars.Count == 0)
                {
                    continue;
                }

                foreach (Ast inner in scriptBlock.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
                {
                    var assignment = (AssignmentStatementAst)inner;
                    VariableExpressionAst? varAst = ExtractAssignmentVariable(assignment);

                    if (varAst is null)
                    {
                        continue;
                    }

                    if (varAst.VariablePath.IsGlobal
                        || varAst.VariablePath.IsScript
                        || varAst.VariablePath.DriveName is not null)
                    {
                        continue;
                    }

                    string varName = varAst.GetNameWithoutScope();

                    if (excluded.Contains(varName) || SpecialVariables.IsSpecialVariable(varName))
                    {
                        continue;
                    }

                    if (outerVars.Contains(varName))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.AvoidVariableShadowingError, varName),
                            varAst.Extent);
                    }
                }
            }
        }

        private static HashSet<string> CollectOuterScopeVariables(ScriptBlockAst scriptBlock)
        {
            var outerVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Ast? current = scriptBlock.Parent;

            while (current is not null)
            {
                if (current is ScriptBlockAst outerBlock)
                {
                    CollectAssignedVariables(outerBlock, outerVars);
                    CollectParameters(outerBlock, outerVars);
                }

                current = current.Parent;
            }

            return outerVars;
        }

        private static void CollectAssignedVariables(ScriptBlockAst scriptBlock, HashSet<string> variables)
        {
            foreach (Ast found in scriptBlock.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)found;
                VariableExpressionAst? varAst = ExtractAssignmentVariable(assignment);

                if (varAst is not null
                    && !varAst.VariablePath.IsGlobal
                    && !varAst.VariablePath.IsScript
                    && varAst.VariablePath.DriveName is null)
                {
                    variables.Add(varAst.GetNameWithoutScope());
                }
            }

            foreach (Ast found in scriptBlock.FindAll(static a => a is ForEachStatementAst, searchNestedScriptBlocks: false))
            {
                variables.Add(((ForEachStatementAst)found).Variable.GetNameWithoutScope());
            }
        }

        private static void CollectParameters(ScriptBlockAst scriptBlock, HashSet<string> variables)
        {
            if (scriptBlock.ParamBlock?.Parameters is not null)
            {
                foreach (ParameterAst param in scriptBlock.ParamBlock.Parameters)
                {
                    variables.Add(param.Name.GetNameWithoutScope());
                }
            }

            if (scriptBlock.Parent is FunctionDefinitionAst funcDef && funcDef.Parameters is not null)
            {
                foreach (ParameterAst param in funcDef.Parameters)
                {
                    variables.Add(param.Name.GetNameWithoutScope());
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
    }
}
