using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    public class ReviewUnusedParameterConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enabled: true);

        public string[] CommandsToTraverse { get; set; } = { "Where-Object", "ForEach-Object" };
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("ReviewUnusedParameter", typeof(Strings), nameof(Strings.ReviewUnusedParameterDescription))]
    public class ReviewUnusedParameter : ConfigurableScriptRule<ReviewUnusedParameterConfiguration>
    {
        public ReviewUnusedParameter(RuleInfo ruleInfo, ReviewUnusedParameterConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var traverseCommands = new HashSet<string>(
                Configuration.CommandsToTraverse ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (Ast node in ast.FindAll(a => a is ScriptBlockAst, searchNestedScriptBlocks: true))
            {
                var scriptBlockAst = (ScriptBlockAst)node;

                if (scriptBlockAst.Find(IsBoundParametersReference, searchNestedScriptBlocks: false) != null)
                {
                    continue;
                }

                IEnumerable<ParameterAst> parameterAsts = scriptBlockAst
                    .FindAll(a => a is ParameterAst, searchNestedScriptBlocks: false)
                    .Cast<ParameterAst>();

                bool hasProcessBlockWithPSItemOrUnderscore = false;
                if (scriptBlockAst.ProcessBlock != null)
                {
                    var processBlockVarCounts = GetVariableCount(scriptBlockAst.ProcessBlock, traverseCommands);
                    processBlockVarCounts.TryGetValue("_", out int underscoreCount);
                    processBlockVarCounts.TryGetValue("psitem", out int psitemCount);
                    if (underscoreCount > 0 || psitemCount > 0)
                    {
                        hasProcessBlockWithPSItemOrUnderscore = true;
                    }
                }

                var variableCount = GetVariableCount(scriptBlockAst, traverseCommands);

                foreach (ParameterAst parameterAst in parameterAsts)
                {
                    var valueFromPipeline = (NamedAttributeArgumentAst)parameterAst.Find(
                        a => a is NamedAttributeArgumentAst named
                            && named.ArgumentName.Equals("ValueFromPipeline", StringComparison.OrdinalIgnoreCase),
                        searchNestedScriptBlocks: false);

                    if (valueFromPipeline != null && GetBoolValue(valueFromPipeline) && hasProcessBlockWithPSItemOrUnderscore)
                    {
                        continue;
                    }

                    string paramName = parameterAst.Name.VariablePath.UserPath;
                    variableCount.TryGetValue(paramName, out int usageCount);
                    if (usageCount >= 2)
                    {
                        continue;
                    }

                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ReviewUnusedParameterError,
                            paramName),
                        parameterAst.Name.Extent);
                }
            }
        }

        private static bool GetBoolValue(NamedAttributeArgumentAst namedAttrAst)
        {
            if (namedAttrAst.ExpressionOmitted)
            {
                return true;
            }

            if (namedAttrAst.Argument is VariableExpressionAst varExpr)
            {
                return varExpr.VariablePath.UserPath.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsBoundParametersReference(Ast ast)
        {
            if (ast is VariableExpressionAst variableAst
                && variableAst.VariablePath.UserPath.Equals("PSBoundParameters", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ast is MemberExpressionAst memberAst
                && memberAst.Member is StringConstantExpressionAst memberStr
                && memberStr.Value.Equals("BoundParameters", StringComparison.OrdinalIgnoreCase))
            {
                if (memberAst.Expression is VariableExpressionAst veAst
                    && veAst.VariablePath.UserPath.Equals("MyInvocation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (memberAst.Expression is MemberExpressionAst nestedMember
                    && nestedMember.Expression is VariableExpressionAst nestedVar
                    && nestedVar.VariablePath.UserPath.Equals("PSCmdlet", StringComparison.OrdinalIgnoreCase)
                    && nestedMember.Member is StringConstantExpressionAst nestedStr
                    && nestedStr.Value.Equals("MyInvocation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, int> GetVariableCount(
            Ast ast,
            HashSet<string> traverseCommands,
            Dictionary<string, int>? data = null)
        {
            var content = data ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var varCounts = ast.FindAll(a => a is VariableExpressionAst, searchNestedScriptBlocks: false)
                .Cast<VariableExpressionAst>()
                .Select(v => v.VariablePath.UserPath)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var entry in varCounts)
            {
                if (content.ContainsKey(entry.Key))
                {
                    content[entry.Key] += entry.Value;
                }
                else
                {
                    content[entry.Key] = entry.Value;
                }
            }

            var traversableScriptBlocks = ast
                .FindAll(a => a is ScriptBlockExpressionAst, searchNestedScriptBlocks: false)
                .Where(a => a.Parent is CommandAst cmd
                    && cmd.CommandElements[0] is StringConstantExpressionAst strConst
                    && traverseCommands.Contains(strConst.Value))
                .Select(a => ((ScriptBlockExpressionAst)a).ScriptBlock);

            foreach (Ast scriptBlock in traversableScriptBlocks)
            {
                if (scriptBlock != ast)
                {
                    GetVariableCount(scriptBlock, traverseCommands, content);
                }
            }

            return content;
        }
    }
}
