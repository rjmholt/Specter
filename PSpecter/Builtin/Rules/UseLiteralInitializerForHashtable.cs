using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseLiteralInitializerForHashtable", typeof(Strings), nameof(Strings.UseLiteralInitilializerForHashtableDescription))]
    internal class UseLiteralInitializerForHashtable : ScriptRule
    {
        private static readonly HashSet<string> s_hashtableTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system.collections.hashtable",
            "collections.hashtable",
            "hashtable",
        };

        internal UseLiteralInitializerForHashtable(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;
                ScriptDiagnostic? diagnostic = AnalyzeNewObjectCommand(cmdAst);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }

            foreach (Ast node in ast.FindAll(static testAst => testAst is InvokeMemberExpressionAst, searchNestedScriptBlocks: true))
            {
                var invokeAst = (InvokeMemberExpressionAst)node;
                ScriptDiagnostic? diagnostic = AnalyzeStaticNewCall(invokeAst);
                if (diagnostic != null)
                {
                    yield return diagnostic;
                }
            }
        }

        private ScriptDiagnostic? AnalyzeNewObjectCommand(CommandAst cmdAst)
        {
            if (cmdAst.CommandElements.Count < 2)
            {
                return null;
            }

            string? commandName = cmdAst.GetCommandName();
            if (commandName == null || !commandName.Equals("New-Object", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? typeName = null;
            var positionalArgs = new List<CommandElementAst>();
            ExpressionAst? argumentListValue = null;

            for (int i = 1; i < cmdAst.CommandElements.Count; i++)
            {
                if (cmdAst.CommandElements[i] is CommandParameterAst paramAst)
                {
                    if (paramAst.ParameterName.StartsWith("TypeName", StringComparison.OrdinalIgnoreCase)
                        && i + 1 < cmdAst.CommandElements.Count
                        && cmdAst.CommandElements[i + 1] is ExpressionAst)
                    {
                        var typeExpr = cmdAst.CommandElements[i + 1] as StringConstantExpressionAst;
                        if (typeExpr is not null)
                        {
                            typeName = typeExpr.Value;
                        }
                        i++;
                    }
                    else if (paramAst.ParameterName.StartsWith("ArgumentList", StringComparison.OrdinalIgnoreCase)
                        && i + 1 < cmdAst.CommandElements.Count
                        && cmdAst.CommandElements[i + 1] is ExpressionAst argExpr)
                    {
                        argumentListValue = argExpr;
                        i++;
                    }
                }
                else
                {
                    positionalArgs.Add(cmdAst.CommandElements[i]);
                }
            }

            if (typeName == null && positionalArgs.Count > 0
                && positionalArgs[0] is StringConstantExpressionAst strConst)
            {
                typeName = strConst.Value;
            }

            if (typeName == null || !s_hashtableTypeNames.Contains(typeName))
            {
                return null;
            }

            if (argumentListValue != null
                && HasIgnoreCaseInExtent(argumentListValue))
            {
                return null;
            }

            foreach (CommandElementAst positionalArg in positionalArgs)
            {
                if (HasIgnoreCaseInExtent(positionalArg))
                {
                    return null;
                }
            }

            return CreateDiagnosticWithCorrection(cmdAst);
        }

        private ScriptDiagnostic? AnalyzeStaticNewCall(InvokeMemberExpressionAst invokeAst)
        {
            if (!(invokeAst.Expression is TypeExpressionAst typeExpr)
                || !s_hashtableTypeNames.Contains(typeExpr.TypeName.FullName))
            {
                return null;
            }

            if (!(invokeAst.Member is StringConstantExpressionAst memberName)
                || !memberName.Value.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (invokeAst.Arguments != null && HasIgnoreCaseComparerArg(invokeAst.Arguments))
            {
                return null;
            }

            return CreateDiagnosticWithCorrection(invokeAst);
        }

        private static bool HasIgnoreCaseInExtent(Ast ast)
        {
            return ast.Extent.Text.EndsWith("ignorecase", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasIgnoreCaseComparerArg(IReadOnlyList<ExpressionAst> arguments)
        {
            foreach (ExpressionAst arg in arguments)
            {
                if (arg is MemberExpressionAst memberExpr
                    && memberExpr.Member is StringConstantExpressionAst strConst
                    && strConst.Value.EndsWith("ignorecase", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private ScriptDiagnostic CreateDiagnosticWithCorrection(Ast violationAst)
        {
            var corrections = new List<Correction>
            {
                new Correction(violationAst.Extent, "@{}", Strings.UseLiteralInitilializerForHashtableDescription)
            };

            return CreateDiagnostic(
                Strings.UseLiteralInitilializerForHashtableDescription,
                violationAst.Extent,
                corrections);
        }
    }
}
