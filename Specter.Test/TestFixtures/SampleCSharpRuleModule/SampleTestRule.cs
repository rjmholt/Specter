using Specter;
using Specter.Rules;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace SampleCSharpRuleModule;

[Rule("SampleTestRule", "Flags all Invoke-Expression calls", Severity = DiagnosticSeverity.Warning)]
public sealed class SampleTestRule : ScriptRule
{
    public SampleTestRule(RuleInfo ruleInfo)
        : base(ruleInfo)
    {
    }

    public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
    {
        IEnumerable<Ast> invokeExpressionCalls = ast.FindAll(
            static node => node is CommandAst cmdAst
                && string.Equals(cmdAst.GetCommandName(), "Invoke-Expression", System.StringComparison.OrdinalIgnoreCase),
            searchNestedScriptBlocks: true);

        foreach (Ast commandAst in invokeExpressionCalls)
        {
            yield return CreateDiagnostic("Avoid using Invoke-Expression in this script.", commandAst.Extent);
        }
    }
}
