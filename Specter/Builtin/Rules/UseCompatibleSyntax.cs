using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using System.Text;
using Specter;
using Specter.Builtin.Rules;
using Specter.Configuration;
using Specter.Rules;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    public record UseCompatibleSyntaxConfiguration : IRuleConfiguration
    {
        public string[] TargetVersions { get; init; } = Array.Empty<string>();
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enable: false);
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCompatibleSyntax", typeof(Strings), nameof(Strings.UseCompatibleSyntaxDescription), Severity = DiagnosticSeverity.Error)]
    internal class UseCompatibleSyntax : ConfigurableScriptRule<UseCompatibleSyntaxConfiguration>
    {
        private static readonly Version s_v3 = new Version(3, 0);
        private static readonly Version s_v4 = new Version(4, 0);
        private static readonly Version s_v5 = new Version(5, 0);
        private static readonly Version s_v6 = new Version(6, 0);
        private static readonly Version s_v7 = new Version(7, 0);

        private static readonly IReadOnlyList<Version> s_targetableVersions = new[]
        {
            s_v3, s_v4, s_v5, s_v6, s_v7,
        };

        internal UseCompatibleSyntax(RuleInfo ruleInfo, UseCompatibleSyntaxConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            HashSet<Version> targetVersions = GetTargetedVersions(Configuration.TargetVersions);
            if (targetVersions.Count == 0)
            {
                yield break;
            }

            var visitor = new SyntaxCompatibilityVisitor(this, scriptPath ?? string.Empty, targetVersions);
            ast.Visit(visitor);
            foreach (ScriptDiagnostic diagnostic in visitor.GetDiagnostics())
            {
                yield return diagnostic;
            }
        }

        private static HashSet<Version> GetTargetedVersions(string[] versionSettings)
        {
            if (versionSettings is null || versionSettings.Length == 0)
            {
                return new HashSet<Version> { s_v5, s_v6, s_v7 };
            }

            var targetVersions = new HashSet<Version>();
            foreach (string versionStr in versionSettings)
            {
                if (!Version.TryParse(versionStr, out Version? version))
                {
                    continue;
                }

                foreach (Version targetableVersion in s_targetableVersions)
                {
                    if (version.Major == targetableVersion.Major)
                    {
                        targetVersions.Add(targetableVersion);
                        break;
                    }
                }
            }
            return targetVersions;
        }

#if CORECLR
        private class SyntaxCompatibilityVisitor : AstVisitor2
#else
        private class SyntaxCompatibilityVisitor : AstVisitor
#endif
        {
            private readonly UseCompatibleSyntax _rule;
            private readonly string _filePath;
            private readonly HashSet<Version> _targetVersions;
            private readonly List<ScriptDiagnostic> _diagnostics;

            internal SyntaxCompatibilityVisitor(
                UseCompatibleSyntax rule,
                string filePath,
                HashSet<Version> targetVersions)
            {
                _rule = rule;
                _filePath = filePath;
                _targetVersions = targetVersions;
                _diagnostics = new List<ScriptDiagnostic>();
            }

            public IReadOnlyList<ScriptDiagnostic> GetDiagnostics() => _diagnostics;

            public override AstVisitAction VisitInvokeMemberExpression(InvokeMemberExpressionAst methodCallAst)
            {
#if CORECLR
                if (methodCallAst.NullConditional && TargetsBelow7())
                {
                    AddDiagnostic(
                        methodCallAst,
                        "null-conditional method invocation",
                        "${x}?.Method()",
                        "3,4,5,6");
                }
#endif

                if (_targetVersions.Contains(s_v3) && methodCallAst.Member is VariableExpressionAst)
                {
                    AddDiagnostic(
                        methodCallAst,
                        "dynamic method invocation",
                        "$x.$method()",
                        "3");
                }

                if (methodCallAst.Expression is TypeExpressionAst typeExpressionAst
                    && methodCallAst.Member is StringConstantExpressionAst memberStr
                    && memberStr.Value.Equals("new", StringComparison.OrdinalIgnoreCase)
                    && (_targetVersions.Contains(s_v3) || _targetVersions.Contains(s_v4)))
                {
                    string typeName = typeExpressionAst.TypeName.FullName;
                    Correction correction = CreateNewObjectCorrection(
                        methodCallAst.Extent,
                        typeName,
                        methodCallAst.Arguments);

                    AddDiagnostic(
                        methodCallAst,
                        "constructor",
                        "[type]::new()",
                        "3,4",
                        correction);
                }

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
            {
                if (functionDefinitionAst.IsWorkflow
                    && (_targetVersions.Contains(s_v6) || _targetVersions.Contains(s_v7)))
                {
                    AddDiagnostic(functionDefinitionAst, "workflow", "workflow { ... }", "6,7");
                }

                return AstVisitAction.Continue;
            }

#if CORECLR
            public override AstVisitAction VisitUsingStatement(UsingStatementAst usingStatementAst)
            {
                if (_targetVersions.Contains(s_v3) || _targetVersions.Contains(s_v4))
                {
                    AddDiagnostic(usingStatementAst, "using statement", "using ...;", "3,4");
                }

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
            {
                if (_targetVersions.Contains(s_v3) || _targetVersions.Contains(s_v4))
                {
                    AddDiagnostic(
                        typeDefinitionAst,
                        "type definition",
                        "class MyClass { ... } | enum MyEnum { ... }",
                        "3,4");
                }

                return AstVisitAction.Continue;
            }
#endif

            public override AstVisitAction VisitMemberExpression(MemberExpressionAst memberExpressionAst)
            {
#if CORECLR
                if (memberExpressionAst.NullConditional && TargetsBelow7())
                {
                    AddDiagnostic(
                        memberExpressionAst,
                        "null-conditional member access",
                        "${x}?.Member",
                        "3,4,5,6");
                }
#endif

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
            {
#if CORECLR
                if (assignmentStatementAst.Operator == TokenKind.QuestionQuestionEquals
                    && TargetsBelow7())
                {
                    AddDiagnostic(assignmentStatementAst, "null-conditional assignment", "$x ??= $y", "3,4,5,6");
                }
#endif

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst)
            {
#if CORECLR
                if (binaryExpressionAst.Operator == TokenKind.QuestionQuestion
                    && TargetsBelow7())
                {
                    AddDiagnostic(binaryExpressionAst, "null-coalescing operator", "$x ?? $y", "3,4,5,6");
                }
#endif

                return AstVisitAction.Continue;
            }

#if CORECLR
            public override AstVisitAction VisitTernaryExpression(TernaryExpressionAst ternaryExpressionAst)
            {
                if (!TargetsBelow7())
                {
                    return AstVisitAction.Continue;
                }

                string replacement = $"if ({ternaryExpressionAst.Condition.Extent.Text}) {{ {ternaryExpressionAst.IfTrue.Extent.Text} }} else {{ {ternaryExpressionAst.IfFalse.Extent.Text} }}";
                var correction = new Correction(
                    ternaryExpressionAst.Extent,
                    replacement,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseCompatibleSyntaxCorrection,
                        "if/else",
                        "3,4,5,6"));

                AddDiagnostic(ternaryExpressionAst, "ternary expression", "<test> ? <exp1> : <exp2>", "3,4,5,6", correction);

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitPipelineChain(PipelineChainAst pipelineChainAst)
            {
                if (TargetsBelow7())
                {
                    AddDiagnostic(
                        pipelineChainAst,
                        "pipeline chain",
                        "<pipeline1> && <pipeline2> OR <pipeline1> || <pipeline2>",
                        "3,4,5,6");
                }

                return AstVisitAction.Continue;
            }
#endif

            private bool TargetsBelow7()
            {
                return _targetVersions.Contains(s_v3)
                    || _targetVersions.Contains(s_v4)
                    || _targetVersions.Contains(s_v5)
                    || _targetVersions.Contains(s_v6);
            }

            private void AddDiagnostic(
                Ast offendingAst,
                string syntaxName,
                string syntaxExample,
                string unsupportedVersions,
                Correction? correction = null)
            {
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UseCompatibleSyntaxError,
                    syntaxName,
                    syntaxExample,
                    unsupportedVersions);

                if (correction is not null)
                {
                    _diagnostics.Add(_rule.CreateDiagnostic(
                        message,
                        offendingAst,
                        new[] { correction }));
                }
                else
                {
                    _diagnostics.Add(_rule.CreateDiagnostic(message, offendingAst));
                }
            }

            private static Correction CreateNewObjectCorrection(
                IScriptExtent offendingExtent,
                string typeName,
                IReadOnlyList<ExpressionAst> argumentAsts)
            {
                var sb = new StringBuilder("New-Object '")
                    .Append(typeName)
                    .Append('\'');

                if (argumentAsts is not null && argumentAsts.Count > 0)
                {
                    sb.Append(" @(");
                    for (int i = 0; i < argumentAsts.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(", ");
                        }
                        sb.Append(argumentAsts[i].Extent.Text);
                    }
                    sb.Append(')');
                }

                return new Correction(
                    offendingExtent,
                    sb.ToString(),
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseCompatibleSyntaxCorrection,
                        "New-Object @($arg1, $arg2, ...)",
                        "3,4"));
            }
        }
    }
}
