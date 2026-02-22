using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseOutputTypeCorrectly", typeof(Strings), nameof(Strings.UseOutputTypeCorrectlyDescription))]
    public class UseOutputTypeCorrectly : ScriptRule
    {
        private static readonly HashSet<string> s_ignoredTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "object",
            "System.Object",
            "void",
            "System.Void",
            "PSObject",
            "System.Management.Automation.PSObject",
            "PSCustomObject",
        };

        public UseOutputTypeCorrectly(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            foreach (Ast node in ast.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;

                if (IsInsideTypeDefinition(funcAst))
                {
                    continue;
                }

                if (!HasCmdletBinding(funcAst))
                {
                    continue;
                }

                HashSet<string> declaredOutputTypes = GetDeclaredOutputTypes(funcAst);
                if (declaredOutputTypes.Count == 0)
                {
                    continue;
                }

                foreach (var returnType in InferReturnTypes(funcAst))
                {
                    string? typeName = NormalizeTypeName(returnType.TypeName);

                    if (typeName is null || s_ignoredTypes.Contains(typeName))
                    {
                        continue;
                    }

                    if (!declaredOutputTypes.Contains(typeName) && returnType.Extent is not null)
                    {
                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UseOutputTypeCorrectlyError,
                                funcAst.Name,
                                returnType.TypeName),
                            returnType.Extent);
                    }
                }
            }
        }

        private static bool IsInsideTypeDefinition(Ast ast)
        {
            Ast parent = ast.Parent;
            while (parent is not null)
            {
                if (parent is TypeDefinitionAst)
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static bool HasCmdletBinding(FunctionDefinitionAst funcAst)
        {
            if (funcAst.Body?.ParamBlock?.Attributes is null)
            {
                return false;
            }

            foreach (AttributeAst attr in funcAst.Body.ParamBlock.Attributes)
            {
                if (attr.TypeName.GetReflectionAttributeType() == typeof(CmdletBindingAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> GetDeclaredOutputTypes(FunctionDefinitionAst funcAst)
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (funcAst.Body?.ParamBlock?.Attributes is null)
            {
                return types;
            }

            foreach (AttributeAst attr in funcAst.Body.ParamBlock.Attributes)
            {
                if (attr.TypeName.GetReflectionAttributeType() != typeof(OutputTypeAttribute))
                {
                    continue;
                }

                foreach (ExpressionAst positionalArg in attr.PositionalArguments)
                {
                    if (positionalArg is StringConstantExpressionAst strConst)
                    {
                        if (NormalizeTypeName(strConst.Value) is string normalized)
                            types.Add(normalized);
                    }
                    else if (positionalArg is TypeExpressionAst typeExpr)
                    {
                        if (NormalizeTypeName(typeExpr.TypeName.FullName) is string normalized)
                            types.Add(normalized);
                    }
                    else if (positionalArg is ArrayLiteralAst arrayLit)
                    {
                        foreach (ExpressionAst elem in arrayLit.Elements)
                        {
                            if (elem is StringConstantExpressionAst elemStr)
                            {
                                if (NormalizeTypeName(elemStr.Value) is string normalized)
                                    types.Add(normalized);
                            }
                            else if (elem is TypeExpressionAst elemType)
                            {
                                if (NormalizeTypeName(elemType.TypeName.FullName) is string normalized)
                                    types.Add(normalized);
                            }
                        }
                    }
                }
            }

            return types;
        }

        private static IEnumerable<ReturnTypeInfo> InferReturnTypes(FunctionDefinitionAst funcAst)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Ast node in funcAst.Body.FindAll(a => true, searchNestedScriptBlocks: false))
            {
                if (node is FunctionDefinitionAst && !ReferenceEquals(node, funcAst))
                {
                    continue;
                }

                string? typeName = null;
                IScriptExtent? extent = null;

                if (node is ReturnStatementAst returnStmt && returnStmt.Pipeline is not null)
                {
                    var inferResult = InferExpressionType(returnStmt.Pipeline);
                    if (inferResult is not null)
                    {
                        typeName = inferResult.Value.TypeName;
                        extent = inferResult.Value.Extent;
                    }
                }
                else if (node is PipelineAst pipeline
                    && IsOutputStatement(pipeline, funcAst))
                {
                    var inferResult = InferPipelineOutputType(pipeline);
                    if (inferResult is not null)
                    {
                        typeName = inferResult.Value.TypeName;
                        extent = inferResult.Value.Extent;
                    }
                }

                if (typeName is not null && !s_ignoredTypes.Contains(typeName) && seen.Add(typeName))
                {
                    yield return new ReturnTypeInfo(typeName!, extent);
                }
            }
        }

        private static bool IsOutputStatement(PipelineAst pipeline, FunctionDefinitionAst funcAst)
        {
            if (pipeline.Parent is NamedBlockAst namedBlock)
            {
                return namedBlock.Parent is ScriptBlockAst scriptBlock
                    && ReferenceEquals(scriptBlock, funcAst.Body);
            }

            if (pipeline.Parent is StatementBlockAst stmtBlock)
            {
                return stmtBlock.Parent is IfStatementAst ifStmt
                    && IsInsideFunction(ifStmt, funcAst);
            }

            return false;
        }

        private static bool IsInsideFunction(Ast node, FunctionDefinitionAst funcAst)
        {
            Ast parent = node.Parent;
            while (parent is not null)
            {
                if (parent is FunctionDefinitionAst parentFunc)
                {
                    return ReferenceEquals(parentFunc, funcAst);
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static ReturnTypeInfo? InferExpressionType(Ast ast)
        {
            if (ast is PipelineAst pipeline)
            {
                return InferPipelineOutputType(pipeline);
            }

            return null;
        }

        private static ReturnTypeInfo? InferPipelineOutputType(PipelineAst pipeline)
        {
            ExpressionAst expr = pipeline.GetPureExpression();
            if (expr is null)
            {
                return null;
            }

            return InferExpressionAstType(expr);
        }

        private static ReturnTypeInfo? InferExpressionAstType(ExpressionAst expr)
        {
            if (expr is HashtableAst)
            {
                return new ReturnTypeInfo("System.Collections.Hashtable", expr.Extent);
            }

            if (expr is StringConstantExpressionAst || expr is ExpandableStringExpressionAst)
            {
                return new ReturnTypeInfo("System.String", expr.Extent);
            }

            if (expr is ConstantExpressionAst constExpr)
            {
                if (constExpr.Value is not null)
                {
                    string? fullName = constExpr.Value.GetType().FullName;
                    if (fullName is not null)
                        return new ReturnTypeInfo(fullName, expr.Extent);
                }
            }

            if (expr is ArrayExpressionAst || expr is ArrayLiteralAst)
            {
                return new ReturnTypeInfo("System.Object[]", expr.Extent);
            }

            if (expr is ConvertExpressionAst convertExpr)
            {
                string? typeName = convertExpr.Type.TypeName.FullName;
                if (typeName is not null && string.Equals(typeName, "pscustomobject", StringComparison.OrdinalIgnoreCase))
                {
                    if (convertExpr.Child is HashtableAst htAst)
                    {
                        string? psTypeName = GetPSTypeName(htAst);
                        if (psTypeName is not null)
                        {
                            return new ReturnTypeInfo(psTypeName!, expr.Extent);
                        }
                    }

                    return null;
                }

                if (typeName is not null)
                    return new ReturnTypeInfo(typeName, expr.Extent);
            }

            return null;
        }

        private static string? GetPSTypeName(HashtableAst hashtable)
        {
            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst key
                    && key.Value is not null
                    && string.Equals(key.Value, "PSTypeName", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Item2 is PipelineAst pipeline)
                    {
                        ExpressionAst valueExpr = pipeline.GetPureExpression();
                        if (valueExpr is StringConstantExpressionAst strValue)
                        {
                            string? val = strValue.Value;
                            if (val is not null)
                                return val!;
                        }
                    }
                }
            }

            return null;
        }

        private static string? NormalizeTypeName(string? typeName)
        {
            if (typeName is null)
            {
                return null;
            }

            if (typeName.StartsWith("[") && typeName.EndsWith("]"))
            {
                typeName = typeName.Substring(1, typeName.Length - 2);
            }

            switch (typeName.ToLowerInvariant())
            {
                case "string": return "System.String";
                case "int": return "System.Int32";
                case "int32": return "System.Int32";
                case "long": return "System.Int64";
                case "int64": return "System.Int64";
                case "double": return "System.Double";
                case "float": return "System.Single";
                case "bool": return "System.Boolean";
                case "hashtable": return "System.Collections.Hashtable";
                case "psobject": return "System.Management.Automation.PSObject";
                case "pscustomobject": return "PSCustomObject";
                default: return typeName;
            }
        }

        private readonly struct ReturnTypeInfo
        {
            public readonly string TypeName;
            public readonly IScriptExtent? Extent;

            public ReturnTypeInfo(string typeName, IScriptExtent? extent)
            {
                TypeName = typeName;
                Extent = extent;
            }
        }
    }
}
