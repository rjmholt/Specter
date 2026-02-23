using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseVerboseMessageInDSCResource", typeof(Strings), nameof(Strings.UseVerboseMessageInDSCResourceDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Information)]
    public class UseVerboseMessageInDscResource : ScriptRule
    {
        public UseVerboseMessageInDscResource(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);

            foreach (FunctionDefinitionAst func in dscFuncs)
            {
                if (!ContainsWriteVerbose(func))
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseVerboseMessageInDSCResourceErrorFunction,
                            func.Name),
                        func.Extent);
                }
            }
        }

        private static bool ContainsWriteVerbose(FunctionDefinitionAst func)
        {
            if (func.Body?.EndBlock is null)
            {
                return false;
            }

            foreach (Ast node in func.Body.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: false))
            {
                var command = (CommandAst)node;
                string? commandName = command.GetCommandName();
                if (commandName != null
                    && commandName.Equals("Write-Verbose", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
