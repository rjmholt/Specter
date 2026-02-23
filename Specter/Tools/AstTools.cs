using Specter.Configuration.Psd;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;

namespace Specter.Tools
{
    public static class AstTools
    {
        private readonly static PsdDataParser s_psdDataParser = new PsdDataParser();

        public static object? GetSafeValueFromAst(ExpressionAst ast)
        {
            return s_psdDataParser.ConvertAstValue(ast);
        }

        /// <summary>
        /// Evaluates an object's truthiness using PowerShell semantics, without
        /// executing any PowerShell code. Mirrors the behavior of
        /// <c>LanguagePrimitives.IsTrue()</c> for the value types we can
        /// statically extract from AST nodes.
        /// </summary>
        public static bool IsTrue(object? value) => value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            decimal m => m != 0,
            string s => s.Length > 0,
            System.Collections.ICollection c => c.Count > 0,
            _ => true,
        };

        public static bool TryGetCmdletBindingAttributeAst(
            IEnumerable<AttributeAst> attributes,
            out AttributeAst? cmdletBindingAttributeAst)
        {
            foreach (var attributeAst in attributes)
            {
                if (attributeAst == null || attributeAst.NamedArguments == null)
                {
                    continue;
                }

                if (attributeAst.TypeName.GetReflectionAttributeType() == typeof(CmdletBindingAttribute))
                {
                    cmdletBindingAttributeAst = attributeAst;
                    return true;
                }
            }

            cmdletBindingAttributeAst = null;
            return false;
        }

        public static bool TryGetShouldProcessAttributeArgumentAst(
            IEnumerable<AttributeAst> attributes,
            out NamedAttributeArgumentAst? shouldProcessArgument)
        {
            if (!TryGetCmdletBindingAttributeAst(attributes, out AttributeAst? cmdletBindingAttributeAst)
                || cmdletBindingAttributeAst is null
                || cmdletBindingAttributeAst.NamedArguments == null
                || cmdletBindingAttributeAst.NamedArguments.Count == 0)
            {
                shouldProcessArgument = null;
                return false;
            }

            foreach (NamedAttributeArgumentAst namedAttributeAst in cmdletBindingAttributeAst!.NamedArguments)
            {
                if (namedAttributeAst.ArgumentName.Equals("SupportsShouldProcess", StringComparison.OrdinalIgnoreCase))
                {
                    shouldProcessArgument = namedAttributeAst;
                    return true;
                }
            }

            shouldProcessArgument = null;
            return false;
        }

        internal static ExpressionAst GetExpressionAstFromScriptAst(Ast ast)
        {
            var scriptBlockAst = (ScriptBlockAst)ast;

            if (scriptBlockAst.EndBlock == null)
            {
                throw new InvalidPowerShellExpressionException("Expected 'end' block in PowerShell input");
            }

            if (scriptBlockAst.EndBlock.Statements == null
                || scriptBlockAst.EndBlock.Statements.Count == 0)
            {
                throw new InvalidPowerShellExpressionException("No statements to parse expression from in input");
            }

            if (scriptBlockAst.EndBlock.Statements.Count != 1)
            {
                throw new InvalidPowerShellExpressionException("Expected a single expression in input");
            }

            if (!(scriptBlockAst.EndBlock.Statements[0] is PipelineAst pipelineAst))
            {
                throw new InvalidPowerShellExpressionException($"Statement '{scriptBlockAst.EndBlock.Statements[0].Extent.Text}' is not a valid expression");
            }

            if (pipelineAst.PipelineElements.Count != 1)
            {
                throw new InvalidPowerShellExpressionException("Expected a single command expression in pipeline");
            }

            if (!(pipelineAst.PipelineElements[0] is CommandExpressionAst commandExpressionAst))
            {
                throw new InvalidPowerShellExpressionException($"Pipeline element '{pipelineAst.PipelineElements[0]}' is not a command expression");
            }

            return commandExpressionAst.Expression;
        }
    }
}
