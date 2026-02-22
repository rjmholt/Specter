using PSpecter.Execution;
using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PSpecter.Suppression
{
    internal sealed class ReferenceEqualityComparerHelper<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparerHelper<T> Instance = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
    public sealed class RuleSuppression
    {
        public RuleSuppression(
            string ruleName,
            string? ruleSuppressionId,
            int startOffset,
            int endOffset,
            int startLineNumber,
            int endLineNumber,
            string? justification)
        {
            RuleName = ruleName;
            RuleSuppressionId = ruleSuppressionId;
            StartOffset = startOffset;
            EndOffset = endOffset;
            StartLineNumber = startLineNumber;
            EndLineNumber = endLineNumber;
            Justification = justification;
        }

        public string RuleName { get; }

        public string? RuleSuppressionId { get; }

        public int StartOffset { get; }

        public int EndOffset { get; }

        public int StartLineNumber { get; }

        public int EndLineNumber { get; }

        public string? Justification { get; }
    }

    public sealed class SuppressedDiagnostic
    {
        public SuppressedDiagnostic(
            ScriptDiagnostic diagnostic,
            IReadOnlyList<RuleSuppression> suppressions)
        {
            Diagnostic = diagnostic;
            Suppressions = suppressions;
        }

        public ScriptDiagnostic Diagnostic { get; }

        public IReadOnlyList<RuleSuppression> Suppressions { get; }
    }

    public sealed class AnalysisResult
    {
        public static readonly AnalysisResult Empty = new(
            Array.Empty<ScriptDiagnostic>(),
            Array.Empty<SuppressedDiagnostic>(),
            Array.Empty<RuleSuppression>(),
            Array.Empty<RuleExecutionError>());

        public AnalysisResult(
            IReadOnlyList<ScriptDiagnostic> diagnostics,
            IReadOnlyList<SuppressedDiagnostic> suppressedDiagnostics,
            IReadOnlyList<RuleSuppression> unappliedSuppressions,
            IReadOnlyList<RuleExecutionError>? ruleErrors = null)
        {
            Diagnostics = diagnostics;
            SuppressedDiagnostics = suppressedDiagnostics;
            UnappliedSuppressions = unappliedSuppressions;
            RuleErrors = ruleErrors ?? Array.Empty<RuleExecutionError>();
        }

        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }

        public IReadOnlyList<SuppressedDiagnostic> SuppressedDiagnostics { get; }

        public IReadOnlyList<RuleSuppression> UnappliedSuppressions { get; }

        public IReadOnlyList<RuleExecutionError> RuleErrors { get; }
    }

    public static class SuppressionParser
    {
        public static Dictionary<string, List<RuleSuppression>> GetSuppressions(Ast scriptAst, Token[]? tokens = null)
        {
            var suppressions = new Dictionary<string, List<RuleSuppression>>(StringComparer.OrdinalIgnoreCase);

            CollectFromAttributes(scriptAst, suppressions);

            if (tokens is not null)
            {
                CommentPragmaParser.CollectFromTokens(tokens, suppressions);
            }

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
                string? scope = GetNamedArgumentValue(attrAst, "Scope");
                string? target = GetNamedArgumentValue(attrAst, "Target");

                string[] ruleNames = ParseRuleNames(category!);

                if (!string.IsNullOrEmpty(scope) && !string.IsNullOrEmpty(target))
                {
                    CollectScopedSuppressions(ast, ruleNames, ruleSuppressionId, justification, scope!, target!, suppressions);
                }
                else
                {
                    Ast? scopeAst = GetSuppressionScope(attrAst);
                    int startOffset = scopeAst?.Extent?.StartOffset ?? 0;
                    int endOffset = scopeAst?.Extent?.EndOffset ?? int.MaxValue;
                    int startLine = scopeAst?.Extent?.StartLineNumber ?? 0;
                    int endLine = scopeAst?.Extent?.EndLineNumber ?? int.MaxValue;

                    AddSuppressions(suppressions, ruleNames, ruleSuppressionId, startOffset, endOffset, startLine, endLine, justification);
                }
            }
        }

        private static void CollectScopedSuppressions(
            Ast rootAst,
            string[] ruleNames,
            string? ruleSuppressionId,
            string? justification,
            string scope,
            string target,
            Dictionary<string, List<RuleSuppression>> suppressions)
        {
            Regex targetRegex = ConvertTargetToRegex(target);

            if (string.Equals(scope, "Function", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Ast node in rootAst.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
                {
                    var funcDef = (FunctionDefinitionAst)node;
                    if (targetRegex.IsMatch(funcDef.Name))
                    {
                        AddSuppressions(
                            suppressions,
                            ruleNames,
                            ruleSuppressionId,
                            funcDef.Extent.StartOffset,
                            funcDef.Extent.EndOffset,
                            funcDef.Extent.StartLineNumber,
                            funcDef.Extent.EndLineNumber,
                            justification);
                    }
                }
            }
            else if (string.Equals(scope, "Class", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Ast node in rootAst.FindAll(a => a is TypeDefinitionAst, searchNestedScriptBlocks: true))
                {
                    var typeDef = (TypeDefinitionAst)node;
                    if (targetRegex.IsMatch(typeDef.Name))
                    {
                        AddSuppressions(
                            suppressions,
                            ruleNames,
                            ruleSuppressionId,
                            typeDef.Extent.StartOffset,
                            typeDef.Extent.EndOffset,
                            typeDef.Extent.StartLineNumber,
                            typeDef.Extent.EndLineNumber,
                            justification);
                    }
                }
            }
        }

        internal static Regex ConvertTargetToRegex(string target)
        {
            if (target.Contains("*") || target.Contains("?"))
            {
                string pattern = "^" + Regex.Escape(target).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            try
            {
                return new Regex(target, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                return new Regex("^" + Regex.Escape(target) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        private static void AddSuppressions(
            Dictionary<string, List<RuleSuppression>> suppressions,
            string[] ruleNames,
            string? ruleSuppressionId,
            int startOffset,
            int endOffset,
            int startLineNumber,
            int endLineNumber,
            string? justification)
        {
            string canonicalName = ruleNames.Length > 0 ? ruleNames[0] : string.Empty;
            var suppression = new RuleSuppression(
                canonicalName,
                ruleSuppressionId,
                startOffset,
                endOffset,
                startLineNumber,
                endLineNumber,
                justification);

            foreach (string ruleName in ruleNames)
            {
                if (!suppressions.TryGetValue(ruleName, out List<RuleSuppression>? list))
                {
                    list = new List<RuleSuppression>();
                    suppressions[ruleName] = list;
                }

                list.Add(suppression);
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

        internal static string? GetStringValue(ExpressionAst expr)
        {
            if (expr is StringConstantExpressionAst strConst)
            {
                return strConst.Value;
            }

            return null;
        }

        internal static string? GetNamedArgumentValue(AttributeAst attr, string argumentName)
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
                Ast? scopeAst = paramBlock.Parent;
                if (scopeAst is ScriptBlockAst && scopeAst.Parent is FunctionDefinitionAst containingFunc)
                {
                    return containingFunc;
                }

                return scopeAst;
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

        internal static string[] ParseRuleNames(string category)
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
                if (FindMatchingSuppressions(diagnostic, suppressions) is not null)
                {
                    continue;
                }

                result.Add(diagnostic);
            }

            return result;
        }

        public static AnalysisResult ApplySuppressionsWithTracking(
            IReadOnlyCollection<ScriptDiagnostic> diagnostics,
            Dictionary<string, List<RuleSuppression>> suppressions,
            IReadOnlyList<RuleExecutionError>? ruleErrors = null)
        {
            if (suppressions.Count == 0)
            {
                return new AnalysisResult(
                    new List<ScriptDiagnostic>(diagnostics),
                    Array.Empty<SuppressedDiagnostic>(),
                    Array.Empty<RuleSuppression>(),
                    ruleErrors);
            }

            var unsuppressed = new List<ScriptDiagnostic>(diagnostics.Count);
            var suppressed = new List<SuppressedDiagnostic>();
            var appliedSuppressions = new HashSet<RuleSuppression>();

            foreach (ScriptDiagnostic diagnostic in diagnostics)
            {
                List<RuleSuppression>? matched = FindMatchingSuppressions(diagnostic, suppressions);
                if (matched is not null)
                {
                    suppressed.Add(new SuppressedDiagnostic(diagnostic, matched));
                    foreach (RuleSuppression s in matched)
                    {
                        appliedSuppressions.Add(s);
                    }
                }
                else
                {
                    unsuppressed.Add(diagnostic);
                }
            }

            var unapplied = new List<RuleSuppression>();
            var seenUnapplied = new HashSet<RuleSuppression>(ReferenceEqualityComparerHelper<RuleSuppression>.Instance);
            foreach (List<RuleSuppression> suppressionList in suppressions.Values)
            {
                foreach (RuleSuppression suppression in suppressionList)
                {
                    if (suppression.RuleSuppressionId is not null
                        && !string.IsNullOrEmpty(suppression.RuleSuppressionId)
                        && !appliedSuppressions.Contains(suppression)
                        && seenUnapplied.Add(suppression))
                    {
                        unapplied.Add(suppression);
                    }
                }
            }

            return new AnalysisResult(unsuppressed, suppressed, unapplied, ruleErrors);
        }

        private static List<RuleSuppression>? FindMatchingSuppressions(
            ScriptDiagnostic diagnostic,
            Dictionary<string, List<RuleSuppression>> suppressions)
        {
            if (diagnostic.Rule is null)
            {
                return null;
            }

            string fullName = diagnostic.Rule.FullName;
            string shortName = diagnostic.Rule.Name;

            List<RuleSuppression>? candidateSuppressions = null;

            if (suppressions.TryGetValue(fullName, out List<RuleSuppression>? fullList))
            {
                candidateSuppressions = fullList;
            }

            if (suppressions.TryGetValue(shortName, out List<RuleSuppression>? shortList))
            {
                if (candidateSuppressions is null)
                {
                    candidateSuppressions = shortList;
                }
                else
                {
                    candidateSuppressions = new List<RuleSuppression>(candidateSuppressions);
                    candidateSuppressions.AddRange(shortList);
                }
            }

            string pssaName = "PS" + shortName;
            if (suppressions.TryGetValue(pssaName, out List<RuleSuppression>? pssaList))
            {
                if (candidateSuppressions is null)
                {
                    candidateSuppressions = pssaList;
                }
                else
                {
                    candidateSuppressions = new List<RuleSuppression>(candidateSuppressions);
                    candidateSuppressions.AddRange(pssaList);
                }
            }

            if (candidateSuppressions is null)
            {
                return null;
            }

            int diagStartOffset = diagnostic.ScriptExtent.StartOffset;
            int diagEndOffset = diagnostic.ScriptExtent.EndOffset;
            int diagStartLine = diagnostic.ScriptExtent.StartLineNumber;
            int diagEndLine = diagnostic.ScriptExtent.EndLineNumber;

            List<RuleSuppression>? matched = null;
            HashSet<RuleSuppression>? seen = null;

            foreach (RuleSuppression suppression in candidateSuppressions)
            {
                bool inScope;
                if (diagStartOffset == 0 && diagEndOffset == 0 && diagStartLine > 0)
                {
                    inScope = diagStartLine >= suppression.StartLineNumber
                        && diagEndLine <= suppression.EndLineNumber;
                }
                else
                {
                    inScope = diagStartOffset >= suppression.StartOffset
                        && diagEndOffset <= suppression.EndOffset;
                }

                if (inScope)
                {
                    bool isMatch;
                    if (suppression.RuleSuppressionId is not null
                        && !string.IsNullOrEmpty(suppression.RuleSuppressionId))
                    {
                        isMatch = string.Equals(
                            suppression.RuleSuppressionId,
                            diagnostic.RuleSuppressionId,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        seen ??= new HashSet<RuleSuppression>(ReferenceEqualityComparerHelper<RuleSuppression>.Instance);
                        if (seen.Add(suppression))
                        {
                            matched ??= new List<RuleSuppression>();
                            matched.Add(suppression);
                        }
                    }
                }
            }

            return matched;
        }
    }
}
