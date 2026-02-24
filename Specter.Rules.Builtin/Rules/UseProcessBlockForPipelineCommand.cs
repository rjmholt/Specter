using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    /// <summary>
    /// UseProcessBlockForPipelineCommand: Check that commands accepting pipeline input use a process block.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseProcessBlockForPipelineCommand", typeof(Strings), nameof(Strings.UseProcessBlockForPipelineCommandDescription))]
    internal class UseProcessBlockForPipelineCommand : ScriptRule
    {
        internal UseProcessBlockForPipelineCommand(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        /// <summary>
        /// AnalyzeScript: Analyze the script to check that commands accepting pipeline input have a process block.
        /// </summary>
        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(Strings.NullAstErrorMessage);
            }

            IEnumerable<Ast> scriptBlockAsts = ast.FindAll(static testAst => testAst is ScriptBlockAst, true);

            foreach (ScriptBlockAst scriptBlockAst in scriptBlockAsts)
            {
                if (scriptBlockAst.ProcessBlock != null
                    || scriptBlockAst.ParamBlock?.Parameters == null)
                {
                    continue;
                }

                foreach (ParameterAst paramAst in scriptBlockAst.ParamBlock.Parameters)
                {
                    bool hasPipelineInput = false;

                    foreach (AttributeBaseAst paramAstAttribute in paramAst.Attributes)
                    {
                        if (paramAstAttribute is not AttributeAst paramAttributeAst)
                        {
                            continue;
                        }

                        if (paramAttributeAst.NamedArguments == null)
                        {
                            continue;
                        }

                        foreach (NamedAttributeArgumentAst namedArgument in paramAttributeAst.NamedArguments)
                        {
                            if (string.Equals(namedArgument.ArgumentName, "valuefrompipeline", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(namedArgument.ArgumentName, "valuefrompipelinebypropertyname", StringComparison.OrdinalIgnoreCase))
                            {
                                hasPipelineInput = true;
                                break;
                            }
                        }

                        if (hasPipelineInput)
                        {
                            break;
                        }
                    }

                    if (hasPipelineInput)
                    {
                        yield return CreateDiagnostic(Strings.UseProcessBlockForPipelineCommandError, paramAst);
                    }
                }
            }
        }
    }
}
