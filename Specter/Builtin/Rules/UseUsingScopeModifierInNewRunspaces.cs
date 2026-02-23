using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseUsingScopeModifierInNewRunspaces", typeof(Strings), nameof(Strings.UseUsingScopeModifierInNewRunspacesDescription))]
    internal class UseUsingScopeModifierInNewRunspaces : ScriptRule
    {
        private static readonly HashSet<string> s_jobCmdletNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Start-Job", "sajb"
        };

        private static readonly HashSet<string> s_threadJobCmdletNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Start-ThreadJob"
        };

        private static readonly HashSet<string> s_foreachObjectCmdletNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ForEach-Object", "foreach", "%"
        };

        private static readonly HashSet<string> s_invokeCommandCmdletNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Invoke-Command", "icm"
        };

        private static readonly HashSet<string> s_inlineScriptCmdletNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "InlineScript"
        };

        private static readonly string[] s_dscScriptResourceCommandNames = { "GetScript", "TestScript", "SetScript" };

        internal UseUsingScopeModifierInNewRunspaces(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var visitor = new NewRunspaceVisitor(this, scriptPath);
            ast.Visit(visitor);
            return visitor.GetDiagnostics();
        }

#if !(PSV3 || PSV4)
        private class NewRunspaceVisitor : AstVisitor2
#else
        private class NewRunspaceVisitor : AstVisitor
#endif
        {
            private readonly UseUsingScopeModifierInNewRunspaces _rule;
            private readonly string? _filePath;
            private readonly List<ScriptDiagnostic> _diagnostics = new List<ScriptDiagnostic>();
            private readonly Dictionary<string, HashSet<string>> _varsDeclaredPerSession = new Dictionary<string, HashSet<string>>();

            internal NewRunspaceVisitor(UseUsingScopeModifierInNewRunspaces rule, string? filePath)
            {
                _rule = rule;
                _filePath = filePath;
            }

            public IEnumerable<ScriptDiagnostic> GetDiagnostics() => _diagnostics;

            public override AstVisitAction VisitScriptBlockExpression(ScriptBlockExpressionAst scriptBlockExpressionAst)
            {
                if (!(scriptBlockExpressionAst.Parent is CommandAst commandAst))
                {
                    return AstVisitAction.Continue;
                }

                string? cmdName = commandAst.GetCommandName();
                if (cmdName is null)
                {
                    return AstVisitAction.SkipChildren;
                }

                int scriptBlockIndex = commandAst.CommandElements.IndexOf(scriptBlockExpressionAst);
                CommandParameterAst? scriptBlockParameterAst = scriptBlockIndex > 0
                    ? commandAst.CommandElements[scriptBlockIndex - 1] as CommandParameterAst
                    : null;

                if (IsInlineScriptBlock(cmdName)
                    || IsJobScriptBlock(cmdName, scriptBlockParameterAst)
                    || IsForeachParallelScriptBlock(cmdName, scriptBlockParameterAst)
                    || IsInvokeCommandComputerScriptBlock(cmdName, commandAst)
                    || IsDscScriptResource(cmdName, commandAst))
                {
                    AnalyzeScriptBlock(scriptBlockExpressionAst);
                    return AstVisitAction.SkipChildren;
                }

                if (IsInvokeCommandSessionScriptBlock(cmdName, commandAst))
                {
                    if (!TryGetSessionName(commandAst, out string? sessionName))
                    {
                        return AstVisitAction.Continue;
                    }

                    IReadOnlyCollection<string> assignedVars = FindAssignedVars(scriptBlockExpressionAst);
                    AddAssignedVarsToSession(sessionName!, assignedVars);

                    foreach (VariableExpressionAst varAst in FindNonUsingNonAssignedVars(scriptBlockExpressionAst, GetSessionVars(sessionName!)))
                    {
                        _diagnostics.Add(CreateDiagnosticForVar(varAst));
                    }

                    return AstVisitAction.SkipChildren;
                }

                return AstVisitAction.Continue;
            }

            private void AnalyzeScriptBlock(ScriptBlockExpressionAst scriptBlockAst)
            {
                IReadOnlyCollection<string> assignedVars = FindAssignedVars(scriptBlockAst);

                foreach (VariableExpressionAst varAst in FindNonUsingNonAssignedVars(scriptBlockAst, assignedVars))
                {
                    _diagnostics.Add(CreateDiagnosticForVar(varAst));
                }
            }

            private ScriptDiagnostic CreateDiagnosticForVar(VariableExpressionAst varAst)
            {
                string varWithUsing = $"$using:{varAst.VariablePath.UserPath}";
                string description = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UseUsingScopeModifierInNewRunspacesCorrectionDescription,
                    varAst.Extent.Text,
                    varWithUsing);

                var correction = new Correction(varAst.Extent, varWithUsing, description);

                return _rule.CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseUsingScopeModifierInNewRunspacesError,
                        varAst.ToString()),
                    varAst.Extent,
                    new[] { correction });
            }

            private static IReadOnlyCollection<string> FindAssignedVars(Ast ast)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Ast node in ast.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: true))
                {
                    var assignment = (AssignmentStatementAst)node;
                    if (TryGetVariableFromExpression(assignment.Left, out VariableExpressionAst? variable) && variable is not null)
                    {
                        result.Add(variable.VariablePath.UserPath);
                    }
                }

                return result;
            }

            private static bool TryGetVariableFromExpression(ExpressionAst expression, out VariableExpressionAst? variableAst)
            {
                switch (expression)
                {
                    case VariableExpressionAst variable:
                        variableAst = variable;
                        return true;

                    case AttributedExpressionAst attributed:
                        return TryGetVariableFromExpression(attributed.Child, out variableAst);

                    default:
                        variableAst = null;
                        return false;
                }
            }

            private static IEnumerable<VariableExpressionAst> FindNonUsingNonAssignedVars(
                Ast ast,
                IReadOnlyCollection<string> assignedVars)
            {
                foreach (Ast node in ast.FindAll(static a => a is VariableExpressionAst, searchNestedScriptBlocks: true))
                {
                    var variable = (VariableExpressionAst)node;

                    if (variable.Parent is UsingExpressionAst)
                    {
                        continue;
                    }

                    if (SpecialVariables.IsSpecialVariable(variable.VariablePath.UserPath))
                    {
                        continue;
                    }

                    string varName = variable.VariablePath.UserPath;
                    if (assignedVars.Contains(varName))
                    {
                        yield break;
                    }

                    yield return variable;
                }
            }

            private IReadOnlyCollection<string> GetSessionVars(string sessionName)
            {
                if (_varsDeclaredPerSession.TryGetValue(sessionName, out HashSet<string>? vars) && vars is not null)
                {
                    return vars;
                }

                return Array.Empty<string>();
            }

            private void AddAssignedVarsToSession(string sessionName, IReadOnlyCollection<string> vars)
            {
                if (!_varsDeclaredPerSession.TryGetValue(sessionName, out HashSet<string>? existingVars))
                {
                    existingVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _varsDeclaredPerSession[sessionName] = existingVars;
                }

                foreach (string var in vars)
                {
                    existingVars.Add(var);
                }
            }

            private static bool TryGetSessionName(CommandAst commandAst, out string? sessionName)
            {
                for (int i = 1; i < commandAst.CommandElements.Count; i++)
                {
                    if (!(commandAst.CommandElements[i] is CommandParameterAst paramAst))
                    {
                        continue;
                    }

                    if (!paramAst.ParameterName.Equals("Session", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (i + 1 >= commandAst.CommandElements.Count)
                    {
                        break;
                    }

                    if (commandAst.CommandElements[i + 1] is VariableExpressionAst varAst)
                    {
                        sessionName = varAst.VariablePath.UserPath;
                        return true;
                    }

                    break;
                }

                sessionName = null;
                return false;
            }

            private static bool IsInlineScriptBlock(string cmdName)
                => s_inlineScriptCmdletNames.Contains(cmdName);

            private static bool IsJobScriptBlock(string cmdName, CommandParameterAst? scriptBlockParamAst)
            {
                if (!s_jobCmdletNames.Contains(cmdName) && !s_threadJobCmdletNames.Contains(cmdName))
                {
                    return false;
                }

                if (scriptBlockParamAst != null
                    && scriptBlockParamAst.ParameterName.StartsWith("ini", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            private static bool IsForeachParallelScriptBlock(string cmdName, CommandParameterAst? scriptBlockParamAst)
            {
                return s_foreachObjectCmdletNames.Contains(cmdName)
                    && scriptBlockParamAst != null
                    && scriptBlockParamAst.ParameterName.StartsWith("pa", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsInvokeCommandComputerScriptBlock(string cmdName, CommandAst commandAst)
            {
                if (!s_invokeCommandCmdletNames.Contains(cmdName))
                {
                    return false;
                }

                foreach (CommandElementAst element in commandAst.CommandElements)
                {
                    if (element is CommandParameterAst paramAst
                        && paramAst.ParameterName.StartsWith("com", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsInvokeCommandSessionScriptBlock(string cmdName, CommandAst commandAst)
            {
                if (!s_invokeCommandCmdletNames.Contains(cmdName))
                {
                    return false;
                }

                foreach (CommandElementAst element in commandAst.CommandElements)
                {
                    if (element is CommandParameterAst paramAst
                        && paramAst.ParameterName.Equals("session", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsDscScriptResource(string cmdName, CommandAst commandAst)
            {
                foreach (string dscName in s_dscScriptResourceCommandNames)
                {
                    if (dscName.Equals(cmdName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (commandAst.CommandElements.Count > 1
                            && commandAst.CommandElements[1].ToString() == "=")
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
