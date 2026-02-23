using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;

namespace Specter.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUnsafeDeserialization", typeof(Strings), nameof(Strings.AvoidUnsafeDeserializationDescription), Severity = DiagnosticSeverity.Information)]
    internal class AvoidUnsafeDeserialization : ScriptRule
    {
        private static readonly HashSet<string> s_dangerousMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Deserialize",
            "DeserializeAsList",
        };

        private static readonly HashSet<string> s_dangerousTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PSSerializer",
            "System.Management.Automation.PSSerializer",
        };

        private static readonly HashSet<string> s_xmlLoadMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Load",
            "Parse",
        };

        private static readonly HashSet<string> s_xmlTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "XmlDocument",
            "System.Xml.XmlDocument",
            "Xml.XmlDocument",
            "XamlReader",
            "System.Windows.Markup.XamlReader",
            "Windows.Markup.XamlReader",
        };

        private readonly IPowerShellCommandDatabase _commandDb;

        internal AvoidUnsafeDeserialization(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
            : base(ruleInfo)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast found in ast.FindAll(static a => a is CommandAst || a is InvokeMemberExpressionAst, searchNestedScriptBlocks: true))
            {
                if (found is CommandAst cmdAst)
                {
                    string? commandName = cmdAst.GetCommandName();
                    if (commandName is not null
                        && _commandDb.IsCommandOrAlias(commandName, "Import-Clixml")
                        && HasNonConstantArgument(cmdAst))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.AvoidUnsafeDeserializationError, "Import-Clixml"),
                            cmdAst.Extent);
                    }
                }
                else if (found is InvokeMemberExpressionAst memberAst)
                {
                    if (IsUnsafeDeserializationCall(memberAst, out string? methodDescription))
                    {
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.AvoidUnsafeDeserializationError, methodDescription),
                            memberAst.Extent);
                    }
                }
            }
        }

        private static bool HasNonConstantArgument(CommandAst cmdAst)
        {
            var elements = cmdAst.CommandElements;
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] is CommandParameterAst)
                {
                    continue;
                }

                if (elements[i] is StringConstantExpressionAst)
                {
                    return false;
                }

                if (elements[i] is ConstantExpressionAst && elements[i] is not ExpandableStringExpressionAst)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool IsUnsafeDeserializationCall(InvokeMemberExpressionAst memberAst, out string? description)
        {
            description = null;

            if (memberAst.Member is not StringConstantExpressionAst memberName)
            {
                return false;
            }

            if (!memberAst.Static)
            {
                return false;
            }

            string? typeName = GetTypeName(memberAst.Expression);
            if (typeName is null)
            {
                return false;
            }

            if (s_dangerousTypes.Contains(typeName) && s_dangerousMethods.Contains(memberName.Value))
            {
                if (HasNonConstantMethodArguments(memberAst))
                {
                    description = $"[{typeName}]::{memberName.Value}()";
                    return true;
                }
            }

            if (s_xmlTypes.Contains(typeName) && s_xmlLoadMethods.Contains(memberName.Value))
            {
                if (HasNonConstantMethodArguments(memberAst))
                {
                    description = $"[{typeName}]::{memberName.Value}()";
                    return true;
                }
            }

            return false;
        }

        private static string? GetTypeName(ExpressionAst expression)
        {
            if (expression is TypeExpressionAst typeExpr)
            {
                return typeExpr.TypeName.FullName;
            }

            return null;
        }

        private static bool HasNonConstantMethodArguments(InvokeMemberExpressionAst memberAst)
        {
            if (memberAst.Arguments is null || memberAst.Arguments.Count == 0)
            {
                return false;
            }

            foreach (ExpressionAst arg in memberAst.Arguments)
            {
                if (arg is StringConstantExpressionAst)
                {
                    continue;
                }

                if (arg is ConstantExpressionAst && arg is not ExpandableStringExpressionAst)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
