using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation.Language;

namespace PSpecter.Suppression
{
    public sealed class RuleSuppression
    {
        public RuleSuppression(
            string ruleName,
            string? ruleSuppressionId,
            int startOffset,
            int endOffset,
            string? justification)
        {
            RuleName = ruleName;
            RuleSuppressionId = ruleSuppressionId;
            StartOffset = startOffset;
            EndOffset = endOffset;
            Justification = justification;
        }

        public string RuleName { get; }

        public string? RuleSuppressionId { get; }

        public int StartOffset { get; }

        public int EndOffset { get; }

        public string? Justification { get; }
    }

    public static class SuppressionParser
    {
        public static Dictionary<string, List<RuleSuppression>> GetSuppressions(Ast scriptAst)
        {
            var suppressions = new Dictionary<string, List<RuleSuppression>>(StringComparer.OrdinalIgnoreCase);

            CollectFromAttributes(scriptAst, suppressions);

            return suppressions;
        }

        private static void CollectFromAttributes(Ast ast, Dictionary<string, List<RuleSuppression>> suppressions)
        {
            foreach (Ast node in ast.FindAll(a => a is AttributeAst, searchNestedScriptBlocks: true))
            {
                var attrAst = (AttributeAst)node;

                if (!IsSuppressMessageAttribute(attrAst))
                {
                    continue;
                }

                if (attrAst.PositionalArguments.Count < 1)
                {
                    continue;
                }

                string? category = GetStringValue(attrAst.PositionalArguments[0]);
                if (category is null)
                {
                    continue;
                }

                string? ruleSuppressionId = attrAst.PositionalArguments.Count >= 2
                    ? GetStringValue(attrAst.PositionalArguments[1])
                    : null;

                string? justification = GetNamedArgumentValue(attrAst, "Justification");

                Ast? scopeAst = GetSuppressionScope(attrAst);
                int startOffset = scopeAst?.Extent?.StartOffset ?? 0;
                int endOffset = scopeAst?.Extent?.EndOffset ?? int.MaxValue;

                string[] ruleNames = ParseRuleNames(category!);
                foreach (string ruleName in ruleNames)
                {
                    var suppression = new RuleSuppression(
                        ruleName,
                        ruleSuppressionId,
                        startOffset,
                        endOffset,
                        justification);

                    if (!suppressions.TryGetValue(ruleName, out List<RuleSuppression>? list))
                    {
                        list = new List<RuleSuppression>();
                        suppressions[ruleName] = list;
                    }

                    list.Add(suppression);
                }
            }
        }

        private static bool IsSuppressMessageAttribute(AttributeAst attrAst)
        {
            string typeName = attrAst.TypeName.FullName;

            return string.Equals(typeName, "SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "SuppressMessage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "System.Diagnostics.CodeAnalysis.SuppressMessage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Diagnostics.CodeAnalysis.SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Diagnostics.CodeAnalysis.SuppressMessage", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetStringValue(ExpressionAst expr)
        {
            if (expr is StringConstantExpressionAst strConst)
            {
                return strConst.Value;
            }

            return null;
        }

        private static string? GetNamedArgumentValue(AttributeAst attr, string argumentName)
        {
            foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
            {
                if (string.Equals(namedArg.ArgumentName, argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    if (namedArg.Argument is StringConstantExpressionAst strConst)
                    {
                        return strConst.Value;
                    }
                }
            }

            return null;
        }

        private static Ast? GetSuppressionScope(AttributeAst attrAst)
        {
            Ast? parent = attrAst.Parent;

            if (parent is ParamBlockAst paramBlock)
            {
                return paramBlock.Parent;
            }

            if (parent is ParameterAst paramAst)
            {
                return paramAst;
            }

            if (parent is FunctionDefinitionAst funcDef)
            {
                return funcDef;
            }

            if (parent is TypeDefinitionAst typeDef)
            {
                return typeDef;
            }

            if (parent is AttributedExpressionAst attrExpr)
            {
                return GetContainingScope(attrExpr);
            }

            return null;
        }

        private static Ast? GetContainingScope(Ast ast)
        {
            Ast? parent = ast.Parent;
            while (parent is not null)
            {
                if (parent is FunctionDefinitionAst || parent is ScriptBlockAst)
                {
                    return parent;
                }

                parent = parent.Parent;
            }

            return null;
        }

        private static string[] ParseRuleNames(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Array.Empty<string>();
            }

            string stripped = category.Trim();
            if (stripped.StartsWith("PS", StringComparison.OrdinalIgnoreCase)
                && stripped.Length > 2
                && char.IsUpper(stripped[2]))
            {
                return new[] { stripped, "PS/" + stripped.Substring(2) };
            }

            if (stripped.Contains("/"))
            {
                int slash = stripped.IndexOf('/');
                return new[] { stripped, stripped.Substring(0, slash) + stripped.Substring(slash + 1) };
            }

            return new[] { stripped };
        }
    }

    public static class SuppressionApplier
    {
        public static IReadOnlyCollection<ScriptDiagnostic> ApplySuppressions(
            IReadOnlyCollection<ScriptDiagnostic> diagnostics,
            Dictionary<string, List<RuleSuppression>> suppressions)
        {
            if (suppressions.Count == 0)
            {
                return diagnostics;
            }

            var result = new List<ScriptDiagnostic>(diagnostics.Count);

            foreach (ScriptDiagnostic diagnostic in diagnostics)
            {
                if (IsSuppressed(diagnostic, suppressions))
                {
                    continue;
                }

                result.Add(diagnostic);
            }

            return result;
        }

        private static bool IsSuppressed(
            ScriptDiagnostic diagnostic,
            Dictionary<string, List<RuleSuppression>> suppressions)
        {
            if (diagnostic.Rule is null)
            {
                return false;
            }

            string fullName = diagnostic.Rule.FullName;
            string shortName = diagnostic.Rule.Name;

            List<RuleSuppression>? matchingSuppressions = null;

            if (suppressions.TryGetValue(fullName, out List<RuleSuppression>? fullList))
            {
                matchingSuppressions = fullList;
            }

            if (suppressions.TryGetValue(shortName, out List<RuleSuppression>? shortList))
            {
                if (matchingSuppressions is null)
                {
                    matchingSuppressions = shortList;
                }
                else
                {
                    matchingSuppressions = new List<RuleSuppression>(matchingSuppressions);
                    matchingSuppressions.AddRange(shortList);
                }
            }

            string pssaName = "PS" + shortName;
            if (suppressions.TryGetValue(pssaName, out List<RuleSuppression>? pssaList))
            {
                if (matchingSuppressions is null)
                {
                    matchingSuppressions = pssaList;
                }
                else
                {
                    matchingSuppressions = new List<RuleSuppression>(matchingSuppressions);
                    matchingSuppressions.AddRange(pssaList);
                }
            }

            if (matchingSuppressions is null)
            {
                return false;
            }

            int diagStart = diagnostic.ScriptExtent.StartOffset;
            int diagEnd = diagnostic.ScriptExtent.EndOffset;

            foreach (RuleSuppression suppression in matchingSuppressions)
            {
                if (diagStart >= suppression.StartOffset && diagEnd <= suppression.EndOffset)
                {
                    if (suppression.RuleSuppressionId is not null)
                    {
                        if (string.Equals(
                            suppression.RuleSuppressionId,
                            diagnostic.RuleSuppressionId,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
