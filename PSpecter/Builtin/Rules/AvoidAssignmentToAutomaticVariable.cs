using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidAssignmentToAutomaticVariable", typeof(Strings), nameof(Strings.AvoidAssignmentToReadOnlyAutomaticVariableDescription), Severity = DiagnosticSeverity.Warning)]
    public class AvoidAssignmentToAutomaticVariable : ScriptRule
    {
        private static readonly HashSet<string> s_readOnlyAutomaticVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "?", "true", "false", "Host", "PSCulture", "Error", "ExecutionContext",
            "Home", "PID", "PSEdition", "PSHome", "PSUICulture", "PSVersionTable", "ShellId"
        };

        private static readonly HashSet<string> s_readOnlyIntroducedInV6 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IsCoreCLR", "IsLinux", "IsMacOS", "IsWindows"
        };

        private static readonly HashSet<string> s_writableAutomaticVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_", "AllNodes", "Args", "ConsoleFilename", "Event", "EventArgs", "EventSubscriber",
            "ForEach", "Input", "Matches", "MyInvocation", "NestedPromptLevel", "Profile",
            "PSBoundParameters", "PsCmdlet", "PSCommandPath", "PSDebugContext", "PSItem",
            "PSScriptRoot", "PSSenderInfo", "Pwd", "ReportErrorShowExceptionClass",
            "ReportErrorShowInnerException", "ReportErrorShowSource", "ReportErrorShowStackTrace",
            "Sender", "StackTrace", "This"
        };

        public AvoidAssignmentToAutomaticVariable(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is AssignmentStatementAst, searchNestedScriptBlocks: true))
            {
                var assignment = (AssignmentStatementAst)node;
                var variable = assignment.Left.Find(
                    testAst => testAst is VariableExpressionAst && testAst.Parent == assignment,
                    searchNestedScriptBlocks: false) as VariableExpressionAst;

                if (variable == null)
                {
                    continue;
                }

                ScriptDiagnostic diagnostic = CheckVariable(variable.VariablePath.UserPath, variable.Extent, fileName);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is ForEachStatementAst, searchNestedScriptBlocks: true))
            {
                var forEach = (ForEachStatementAst)node;
                var variable = forEach.Variable;
                ScriptDiagnostic diagnostic = CheckVariable(variable.VariablePath.UserPath, variable.Extent, fileName);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is ParameterAst, searchNestedScriptBlocks: true))
            {
                var parameter = (ParameterAst)node;
                var variable = parameter.Find(
                    testAst => testAst is VariableExpressionAst,
                    searchNestedScriptBlocks: false) as VariableExpressionAst;

                if (variable == null)
                {
                    continue;
                }

                if (variable.Parent is NamedAttributeArgumentAst || variable.Parent is AttributeAst)
                {
                    continue;
                }

                ScriptDiagnostic diagnostic = CheckVariable(variable.VariablePath.UserPath, variable.Extent, fileName);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }
        }

        private ScriptDiagnostic CheckVariable(string variableName, IScriptExtent extent, string fileName)
        {
            string suppressionId = variableName;
            ScriptDiagnostic diagnostic = null;

            if (s_readOnlyAutomaticVariables.Contains(variableName))
            {
                diagnostic = CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidAssignmentToReadOnlyAutomaticVariableError, variableName),
                    extent,
                    DiagnosticSeverity.Error);
            }
            else if (s_readOnlyIntroducedInV6.Contains(variableName))
            {
                DiagnosticSeverity severity = IsPowerShell6OrGreater() ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
                diagnostic = CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidAssignmentToReadOnlyAutomaticVariableIntroducedInPowerShell6_0Error, variableName),
                    extent,
                    severity);
            }
            else if (s_writableAutomaticVariables.Contains(variableName))
            {
                diagnostic = CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.AvoidAssignmentToWritableAutomaticVariableError, variableName),
                    extent,
                    DiagnosticSeverity.Warning);
            }

            if (diagnostic is not null)
            {
                diagnostic.RuleSuppressionId = suppressionId;
            }

            return diagnostic;
        }

        private static bool IsPowerShell6OrGreater()
        {
#if CORECLR
            return true;
#else
            return false;
#endif
        }
    }
}
