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
    [Rule("AvoidUsingWriteHost", typeof(Strings), nameof(Strings.AvoidUsingWriteHostDescription))]
    internal class AvoidUsingWriteHost : ScriptRule
    {
        internal AvoidUsingWriteHost(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var visitor = new WriteHostVisitor(this, scriptPath);
            ast.Visit(visitor);
            return visitor.Diagnostics;
        }

        private class WriteHostVisitor : AstVisitor
        {
            private readonly AvoidUsingWriteHost _rule;
            private readonly string? _scriptPath;
            private readonly List<ScriptDiagnostic> _diagnostics = new List<ScriptDiagnostic>();

            internal WriteHostVisitor(AvoidUsingWriteHost rule, string? scriptPath)
            {
                _rule = rule;
                _scriptPath = scriptPath;
            }

            public IReadOnlyList<ScriptDiagnostic> Diagnostics => _diagnostics;

            public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst funcAst)
            {
                if (funcAst.Name.StartsWith("Show", StringComparison.OrdinalIgnoreCase))
                {
                    return AstVisitAction.SkipChildren;
                }

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitCommand(CommandAst cmdAst)
            {
                string commandName = cmdAst.GetCommandName();
                if (string.Equals(commandName, "Write-Host", StringComparison.OrdinalIgnoreCase))
                {
                    string message = string.IsNullOrWhiteSpace(_scriptPath)
                        ? Strings.AvoidUsingWriteHostErrorScriptDefinition
                        : string.Format(CultureInfo.CurrentCulture, Strings.AvoidUsingWriteHostError, System.IO.Path.GetFileName(_scriptPath));

                    _diagnostics.Add(_rule.CreateDiagnostic(message, cmdAst));
                }

                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitInvokeMemberExpression(InvokeMemberExpressionAst imeAst)
            {
                if (imeAst.Expression is TypeExpressionAst typeExpr
                    && typeExpr.TypeName.FullName.EndsWith("Console", StringComparison.OrdinalIgnoreCase)
                    && imeAst.Member.Extent.Text.StartsWith("Write", StringComparison.OrdinalIgnoreCase))
                {
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AvoidUsingConsoleWriteError,
                        string.IsNullOrWhiteSpace(_scriptPath) ? "Script definition" : System.IO.Path.GetFileName(_scriptPath),
                        imeAst.Member.Extent.Text);

                    _diagnostics.Add(_rule.CreateDiagnostic(message, imeAst));
                }

                return AstVisitAction.Continue;
            }
        }
    }
}
