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
    [Rule("SuggestCmdletBinding", "Suggests adding [CmdletBinding()] to exported module functions with parameters.", Severity = DiagnosticSeverity.Information)]
    internal sealed class SuggestCmdletBinding : ScriptRule
    {
        internal SuggestCmdletBinding(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            ScriptModel scriptModel = ScriptModel.GetOrCreate(ast, tokens, scriptPath);
            for (int i = 0; i < scriptModel.Functions.Count; i++)
            {
                FunctionSymbol function = scriptModel.Functions[i];
                if (!function.IsInModule || !function.IsExported || function.IsNested || function.HasCmdletBinding)
                {
                    continue;
                }

                if (function.Parameters.Count == 0)
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(CultureInfo.CurrentCulture, "Function '{0}' is exported and has parameters; consider adding [CmdletBinding()].", function.Name),
                    function.Ast.GetFunctionNameExtent(tokens) ?? function.Ast.Extent,
                    DiagnosticSeverity.Information);
            }
        }
    }
}
