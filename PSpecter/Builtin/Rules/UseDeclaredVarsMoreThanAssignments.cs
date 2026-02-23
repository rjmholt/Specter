using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Globalization;
using System.Linq;
using PSpecter.Rules;
using PSpecter;
using PSpecter.Builtin.Rules;
using PSpecter.CommandDatabase;
using PSpecter.Tools;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    /// <summary>
    /// UseDeclaredVarsMoreThanAssignments: Analyzes the ast to check that variables are used in more than just their assignment.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseDeclaredVarsMoreThanAssignments", typeof(Strings), nameof(Strings.UseDeclaredVarsMoreThanAssignmentsDescription))]
    public class UseDeclaredVarsMoreThanAssignments : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        public UseDeclaredVarsMoreThanAssignments(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb) : base(ruleInfo)
        {
            _commandDb = commandDb;
        }

        /// <summary>
        /// AnalyzeScript: Analyzes the ast to check that variables are used in more than just there assignment.
        /// </summary>
        /// <param name="ast">The script's ast</param>
        /// <param name="fileName">The script's file name</param>
        /// <returns>A List of results from this rule</returns>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            var scriptBlockAsts = ast.FindAll(static x => x is ScriptBlockAst, true);

            if (scriptBlockAsts == null)
            {
                yield break;
            }

            foreach (var scriptBlockAst in scriptBlockAsts)
            {
                var sbAst = scriptBlockAst as ScriptBlockAst;
                foreach (var diagnosticRecord in AnalyzeScriptBlockAst(sbAst!, scriptPath))
                {
                    yield return diagnosticRecord;
                }
            }
        }

        /// <summary>
        /// Checks if a variable is initialized and referenced in either its assignment or children scopes
        /// </summary>
        /// <param name="scriptBlockAst">Ast of type ScriptBlock</param>
        /// <param name="fileName">Name of file containing the ast</param>
        /// <returns>An enumerable containing diagnostic records</returns>
        private IEnumerable<ScriptDiagnostic> AnalyzeScriptBlockAst(ScriptBlockAst scriptBlockAst, string? fileName)
        {
            IEnumerable<Ast> assignmentAsts = scriptBlockAst.FindAll(static testAst => testAst is AssignmentStatementAst, false);
            IEnumerable<Ast> varAsts = scriptBlockAst.FindAll(static testAst => testAst is VariableExpressionAst, true);
            IEnumerable<Ast>? varsInAssignment = null;

            Dictionary<string, AssignmentStatementAst> assignmentsDictionary_OrdinalIgnoreCase = new Dictionary<string, AssignmentStatementAst>(StringComparer.OrdinalIgnoreCase);

            string varKey;
            bool inAssignment;

            if (assignmentAsts == null)
            {
                yield break;
            }

            foreach (AssignmentStatementAst assignmentAst in assignmentAsts)
            {
                // Only checks for the case where lhs is a variable. Ignore things like $foo.property
                VariableExpressionAst? assignmentVarAst = assignmentAst.Left as VariableExpressionAst;

                if (assignmentVarAst == null)
                {
                    // If the variable is declared in a strongly typed way, e.g. [string]$s = 'foo' then the type is ConvertExpressionAst.
                    // Therefore we need to the VariableExpressionAst from its Child property.
                    var assignmentVarAstAsConvertExpressionAst = assignmentAst.Left as ConvertExpressionAst;
                    if (assignmentVarAstAsConvertExpressionAst != null && assignmentVarAstAsConvertExpressionAst.Child != null)
                    {
                        assignmentVarAst = assignmentVarAstAsConvertExpressionAst.Child as VariableExpressionAst;
                    }
                }

                if (assignmentVarAst != null)
                {
                    // Ignore if variable is global or environment variable or scope is drive qualified variable
                    if (!assignmentVarAst.VariablePath.IsScript
                        && !assignmentVarAst.VariablePath.IsGlobal
                        && assignmentVarAst.VariablePath.DriveName == null)
                    {
                        string variableName = assignmentVarAst.GetNameWithoutScope();

                        if (!assignmentsDictionary_OrdinalIgnoreCase.ContainsKey(variableName))
                        {
                            assignmentsDictionary_OrdinalIgnoreCase.Add(variableName, assignmentAst);
                        }
                    }
                }
            }

            if (varAsts != null)
            {
                foreach (VariableExpressionAst varAst in varAsts)
                {
                    varKey = varAst.GetNameWithoutScope();
                    inAssignment = false;

                    if (assignmentsDictionary_OrdinalIgnoreCase.ContainsKey(varKey))
                    {
                        varsInAssignment = assignmentsDictionary_OrdinalIgnoreCase[varKey].Left.FindAll(static testAst => testAst is VariableExpressionAst, true);

                        // Checks if this variableAst is part of the logged assignment
                        foreach (VariableExpressionAst varInAssignment in varsInAssignment)
                        {
                            // Try casting to AssignmentStatementAst to be able to catch case where a variable is assigned more than once (https://github.com/PowerShell/PSScriptAnalyzer/issues/833)
                            var varInAssignmentAsStatementAst = varInAssignment.Parent as AssignmentStatementAst;
                            var varAstAsAssignmentStatementAst = varAst.Parent as AssignmentStatementAst;
                            if (varAstAsAssignmentStatementAst != null)
                            {
                                if (varAstAsAssignmentStatementAst.Operator == TokenKind.Equals)
                                {
                                    if (varInAssignmentAsStatementAst != null)
                                    {
                                        inAssignment = varInAssignmentAsStatementAst.Left.Extent.Text.Equals(varAstAsAssignmentStatementAst.Left.Extent.Text, StringComparison.OrdinalIgnoreCase);
                                    }
                                    else
                                    {
                                        inAssignment = varInAssignment.Equals(varAst);
                                    }
                                }
                            }
                            else
                            {
                                inAssignment = varInAssignment.Equals(varAst);
                            }
                        }

                        if (!inAssignment)
                        {
                            assignmentsDictionary_OrdinalIgnoreCase.Remove(varKey);
                        }

                        // Check if variable belongs to PowerShell built-in variables
                        if (SpecialVariables.IsSpecialVariable(varKey))
                        {
                            assignmentsDictionary_OrdinalIgnoreCase.Remove(varKey);
                        }
                    }
                }
            }

            AnalyzeGetVariableCommands(scriptBlockAst, assignmentsDictionary_OrdinalIgnoreCase);

            foreach (string key in assignmentsDictionary_OrdinalIgnoreCase.Keys)
            {
                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, Strings.UseDeclaredVarsMoreThanAssignmentsError, key),
                    assignmentsDictionary_OrdinalIgnoreCase[key].Left);
            }
        }

        /// <summary>
        /// Detects variables retrieved by usage of Get-Variable and remove those
        /// variables from the entries in <paramref name="assignmentsDictionary_OrdinalIgnoreCase"/>.
        /// </summary>
        /// <param name="scriptBlockAst"></param>
        /// <param name="assignmentsDictionary_OrdinalIgnoreCase"></param>
        private void AnalyzeGetVariableCommands(
            ScriptBlockAst scriptBlockAst,
            Dictionary<string, AssignmentStatementAst> assignmentsDictionary_OrdinalIgnoreCase)
        {
            IEnumerable<Ast> getVariableCommandAsts = scriptBlockAst.FindAll(
                testAst => testAst is CommandAst commandAst
                    && _commandDb.IsCommandOrAlias(commandAst.GetCommandName(), "Get-Variable"),
                searchNestedScriptBlocks: true);

            foreach (CommandAst getVariableCommandAst in getVariableCommandAsts)
            {
                var commandElements = getVariableCommandAst.CommandElements.ToList();

                // Only handle the simplest forms: `Get-Variable name` or `Get-Variable -Name name`
                if (commandElements.Count < 2 || commandElements.Count > 3)
                {
                    continue;
                }

                var commandElementAstOfVariableName = commandElements[commandElements.Count - 1];

                if (commandElements.Count == 3)
                {
                    if (commandElements[1] is not CommandParameterAst commandParameterAst)
                    {
                        continue;
                    }

                    // -Name is the only Get-Variable parameter starting with 'n'
                    if (!commandParameterAst.ParameterName.StartsWith("n", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (commandElementAstOfVariableName is StringConstantExpressionAst constantExpressionAst)
                {
                    assignmentsDictionary_OrdinalIgnoreCase.Remove(constantExpressionAst.Value);
                    continue;
                }

                if (commandElementAstOfVariableName is not ArrayLiteralAst arrayLiteralAst)
                {
                    continue;
                }

                foreach (ExpressionAst expressionAst in arrayLiteralAst.Elements)
                {
                    if (expressionAst is StringConstantExpressionAst constantExpressionAstOfArray)
                    {
                        assignmentsDictionary_OrdinalIgnoreCase.Remove(constantExpressionAstOfArray.Value);
                    }
                }
            }
        }
    }
}
