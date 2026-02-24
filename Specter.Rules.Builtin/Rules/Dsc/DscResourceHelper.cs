using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Specter.Rules.Builtin.Rules.Dsc
{
    internal static class DscResourceHelper
    {
        private static readonly string[] s_targetResourceFunctions =
        {
            "Get-TargetResource",
            "Set-TargetResource",
            "Test-TargetResource",
        };

        private static readonly string[] s_classResourceMethods =
        {
            "Get",
            "Set",
            "Test",
        };

        internal static IReadOnlyList<FunctionDefinitionAst> GetDscResourceFunctions(Ast ast)
        {
            var results = new List<FunctionDefinitionAst>();
            foreach (Ast node in ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var func = (FunctionDefinitionAst)node;
                foreach (string name in s_targetResourceFunctions)
                {
                    if (func.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(func);
                        break;
                    }
                }
            }
            return results;
        }

        internal static IReadOnlyList<TypeDefinitionAst> GetDscClasses(Ast ast)
        {
            var results = new List<TypeDefinitionAst>();
            foreach (Ast node in ast.FindAll(static a => a is TypeDefinitionAst, searchNestedScriptBlocks: true))
            {
                var typeDef = (TypeDefinitionAst)node;
                if (!typeDef.IsClass)
                {
                    continue;
                }

                foreach (AttributeAst attr in typeDef.Attributes)
                {
                    if (attr.TypeName.Name.Equals("DscResource", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(typeDef);
                        break;
                    }
                }
            }
            return results;
        }

        internal static FunctionMemberAst? FindClassMethod(TypeDefinitionAst dscClass, string methodName)
        {
            foreach (MemberAst member in dscClass.Members)
            {
                if (member is FunctionMemberAst method
                    && method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                {
                    return method;
                }
            }
            return null;
        }

        internal static string[] TargetResourceFunctionNames => s_targetResourceFunctions;

        internal static string[] ClassResourceMethodNames => s_classResourceMethods;

        internal static bool IsDscResourceFile(Ast ast)
        {
            return GetDscResourceFunctions(ast).Count > 0 || GetDscClasses(ast).Count > 0;
        }
    }
}
