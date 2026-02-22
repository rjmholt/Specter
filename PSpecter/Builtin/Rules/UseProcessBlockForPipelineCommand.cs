using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    /// <summary>
    /// UseProcessBlockForPipelineCommand: Check that commands accepting pipeline input use a process block.
    /// </summary>
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseProcessBlockForPipelineCommand", typeof(Strings), nameof(Strings.UseProcessBlockForPipelineCommandDescription))]
    public class UseProcessBlockForPipelineCommand : ScriptRule
    {
        public UseProcessBlockForPipelineCommand(RuleInfo ruleInfo)
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

            IEnumerable<Ast> scriptBlockAsts = ast.FindAll(testAst => testAst is ScriptBlockAst, true);

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
