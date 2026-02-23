using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Reflection;

namespace PSpecter.Builtin.Rules.Dsc
{
    internal readonly struct OutputInfo
    {
        public readonly string? TypeName;
        public readonly IScriptExtent Extent;

        internal OutputInfo(string? typeName, IScriptExtent extent)
        {
            TypeName = typeName;
            Extent = extent;
        }
    }

    internal static class PipelineOutputAnalyzer
    {
        private static readonly HashSet<string> s_skipTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Unreached",
            "Undetermined",
            "object",
            "System.Object",
        };

        internal static IReadOnlyList<OutputInfo> GetOutputs(
            FunctionDefinitionAst func,
            IReadOnlyList<TypeDefinitionAst>? classes = null)
        {
            var assignments = BuildAssignmentMap(func, classes);
            var outputs = new List<OutputInfo>();
            CollectOutputs(func.Body, assignments, classes, outputs);
            return outputs;
        }

        internal static IReadOnlyList<OutputInfo> GetReturnStatementOutputs(
            FunctionMemberAst method,
            TypeDefinitionAst containingClass,
            IReadOnlyList<TypeDefinitionAst> classes)
        {
            var assignments = BuildClassMethodAssignmentMap(method, containingClass, classes);
            var outputs = new List<OutputInfo>();

            foreach (Ast node in method.Body.FindAll(static a => a is ReturnStatementAst, searchNestedScriptBlocks: false))
            {
                var ret = (ReturnStatementAst)node;
                if (ret.Pipeline is null)
                {
                    outputs.Add(new OutputInfo(null, ret.Extent));
                    continue;
                }

                string? type = InferStatementType(ret.Pipeline, assignments, classes);
                outputs.Add(new OutputInfo(type, ret.Pipeline.Extent));
            }

            return outputs;
        }

        internal static bool ShouldSkipType(string? typeName, string expectedType)
        {
            return string.IsNullOrEmpty(typeName)
                   || s_skipTypes.Contains(typeName)
                   || string.Equals(typeName, expectedType, StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectOutputs(
            ScriptBlockAst body,
            Dictionary<string, string> assignments,
            IReadOnlyList<TypeDefinitionAst>? classes,
            List<OutputInfo> outputs)
        {
            if (body.EndBlock is not null)
            {
                CollectFromStatementBlock(body.EndBlock.Statements, assignments, classes, outputs);
            }

            if (body.BeginBlock is not null)
            {
                CollectFromStatementBlock(body.BeginBlock.Statements, assignments, classes, outputs);
            }

            if (body.ProcessBlock is not null)
            {
                CollectFromStatementBlock(body.ProcessBlock.Statements, assignments, classes, outputs);
            }
        }

        private static void CollectFromStatementBlock(
            IReadOnlyList<StatementAst> statements,
            Dictionary<string, string> assignments,
            IReadOnlyList<TypeDefinitionAst>? classes,
            List<OutputInfo> outputs)
        {
            foreach (StatementAst stmt in statements)
            {
                CollectFromStatement(stmt, assignments, classes, outputs);
            }
        }

        private static void CollectFromStatement(
            StatementAst stmt,
            Dictionary<string, string> assignments,
            IReadOnlyList<TypeDefinitionAst>? classes,
            List<OutputInfo> outputs)
        {
            switch (stmt)
            {
                case AssignmentStatementAst:
                    break;

                case PipelineAst pipeline:
                    ExpressionAst expr = pipeline.GetPureExpression();
                    if (expr is not null)
                    {
                        string? type = InferExpressionType(expr, assignments, classes);
                        outputs.Add(new OutputInfo(type, stmt.Extent));
                    }
                    break;

                case ReturnStatementAst ret:
                    if (ret.Pipeline is not null)
                    {
                        string? retType = InferStatementType(ret.Pipeline, assignments, classes);
                        outputs.Add(new OutputInfo(retType, ret.Extent));
                    }
                    break;

                case IfStatementAst ifStmt:
                    foreach (var clause in ifStmt.Clauses)
                    {
                        CollectFromStatementBlock(clause.Item2.Statements, assignments, classes, outputs);
                    }
                    if (ifStmt.ElseClause is not null)
                    {
                        CollectFromStatementBlock(ifStmt.ElseClause.Statements, assignments, classes, outputs);
                    }
                    break;

                case ForEachStatementAst forEach:
                    if (forEach.Body is not null)
                    {
                        CollectFromStatementBlock(forEach.Body.Statements, assignments, classes, outputs);
                    }
                    break;

                case ForStatementAst forStmt:
                    if (forStmt.Body is not null)
                    {
                        CollectFromStatementBlock(forStmt.Body.Statements, assignments, classes, outputs);
                    }
                    break;

                case WhileStatementAst whileStmt:
                    if (whileStmt.Body is not null)
                    {
                        CollectFromStatementBlock(whileStmt.Body.Statements, assignments, classes, outputs);
                    }
                    break;

                case DoWhileStatementAst doWhile:
                    if (doWhile.Body is not null)
                    {
                        CollectFromStatementBlock(doWhile.Body.Statements, assignments, classes, outputs);
                    }
                    break;

                case TryStatementAst tryStmt:
                    if (tryStmt.Body is not null)
                    {
                        CollectFromStatementBlock(tryStmt.Body.Statements, assignments, classes, outputs);
                    }
                    foreach (CatchClauseAst catchClause in tryStmt.CatchClauses)
                    {
                        CollectFromStatementBlock(catchClause.Body.Statements, assignments, classes, outputs);
                    }
                    if (tryStmt.Finally is not null)
                    {
                        CollectFromStatementBlock(tryStmt.Finally.Statements, assignments, classes, outputs);
                    }
                    break;

                case SwitchStatementAst switchStmt:
                    foreach (var clause in switchStmt.Clauses)
                    {
                        CollectFromStatementBlock(clause.Item2.Statements, assignments, classes, outputs);
                    }
                    if (switchStmt.Default is not null)
                    {
                        CollectFromStatementBlock(switchStmt.Default.Statements, assignments, classes, outputs);
                    }
                    break;
            }
        }

        private static string? InferStatementType(
            StatementAst stmt,
            Dictionary<string, string> assignments,
            IReadOnlyList<TypeDefinitionAst>? classes)
        {
            if (stmt is PipelineAst pipeline)
            {
                ExpressionAst expr = pipeline.GetPureExpression();
                if (expr is not null)
                {
                    return InferExpressionType(expr, assignments, classes);
                }
            }

            if (stmt is CommandExpressionAst cmdExpr)
            {
                return InferExpressionType(cmdExpr.Expression, assignments, classes);
            }

            return null;
        }

        internal static string? InferExpressionType(
            ExpressionAst expr,
            Dictionary<string, string> assignments,
            IReadOnlyList<TypeDefinitionAst>? classes)
        {
            switch (expr)
            {
                case HashtableAst:
                    return "System.Collections.Hashtable";

                case StringConstantExpressionAst:
                case ExpandableStringExpressionAst:
                    return "System.String";

                case ConstantExpressionAst constExpr:
                    return constExpr.Value?.GetType()?.FullName;

                case ArrayExpressionAst:
                case ArrayLiteralAst:
                    return "System.Object[]";

                case ConvertExpressionAst convertExpr:
                    return ResolveTypeName(convertExpr.Type.TypeName);

                case BinaryExpressionAst binExpr:
                    return InferBinaryExpressionType(binExpr);

                case UnaryExpressionAst unaryExpr:
                    return InferUnaryExpressionType(unaryExpr);

                case ParenExpressionAst parenExpr:
                    if (parenExpr.Pipeline is PipelineAst innerPipeline)
                    {
                        ExpressionAst inner = innerPipeline.GetPureExpression();
                        if (inner is not null)
                        {
                            return InferExpressionType(inner, assignments, classes);
                        }
                    }
                    return null;

                case VariableExpressionAst varExpr:
                    string varName = varExpr.VariablePath.UserPath;
                    if (varName.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || varName.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        return "System.Boolean";
                    }
                    if (varName.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                    if (assignments is not null
                        && assignments.TryGetValue(varName, out string? varType))
                    {
                        return varType;
                    }
                    return null;

                case InvokeMemberExpressionAst invokeMember:
                    return InferInvokeMemberType(invokeMember, classes);

                case MemberExpressionAst memberExpr:
                    return InferMemberAccessType(memberExpr, classes);

                default:
                    return null;
            }
        }

        private static string? InferBinaryExpressionType(BinaryExpressionAst binExpr)
        {
            switch (binExpr.Operator)
            {
                case TokenKind.And:
                case TokenKind.Or:
                case TokenKind.Xor:
                case TokenKind.Ieq:
                case TokenKind.Ine:
                case TokenKind.Igt:
                case TokenKind.Ilt:
                case TokenKind.Ige:
                case TokenKind.Ile:
                case TokenKind.Ceq:
                case TokenKind.Cne:
                case TokenKind.Cgt:
                case TokenKind.Clt:
                case TokenKind.Cge:
                case TokenKind.Cle:
                case TokenKind.Imatch:
                case TokenKind.Inotmatch:
                case TokenKind.Cmatch:
                case TokenKind.Cnotmatch:
                case TokenKind.Ilike:
                case TokenKind.Inotlike:
                case TokenKind.Clike:
                case TokenKind.Cnotlike:
                case TokenKind.Is:
                case TokenKind.IsNot:
                case TokenKind.In:
                case TokenKind.Icontains:
                case TokenKind.Inotcontains:
                    return "System.Boolean";

                default:
                    return null;
            }
        }

        private static string? InferUnaryExpressionType(UnaryExpressionAst unaryExpr)
        {
            switch (unaryExpr.TokenKind)
            {
                case TokenKind.Not:
                case TokenKind.Exclaim:
                    return "System.Boolean";

                case TokenKind.PlusPlus:
                case TokenKind.MinusMinus:
                case TokenKind.PostfixPlusPlus:
                case TokenKind.PostfixMinusMinus:
                    return "System.Int32";

                default:
                    return null;
            }
        }

        private static string? InferInvokeMemberType(
            InvokeMemberExpressionAst invokeMember,
            IReadOnlyList<TypeDefinitionAst>? classes)
        {
            if (invokeMember.Expression is TypeExpressionAst typeExpr && invokeMember.Member is StringConstantExpressionAst methodName)
            {
                return ResolveStaticMethodReturnType(typeExpr.TypeName, methodName.Value!);
            }

            if (invokeMember.Expression is VariableExpressionAst varExpr
                && varExpr.VariablePath.UserPath.Equals("this", StringComparison.OrdinalIgnoreCase)
                && invokeMember.Member is StringConstantExpressionAst memberName
                && classes is not null)
            {
                foreach (TypeDefinitionAst cls in classes)
                {
                    FunctionMemberAst? method = DscResourceHelper.FindClassMethod(cls, memberName.Value!);
                    if (method is not null && method.ReturnType is not null)
                    {
                        return ResolveTypeName(method.ReturnType.TypeName);
                    }
                }
            }

            return null;
        }

        private static string? InferMemberAccessType(
            MemberExpressionAst memberExpr,
            IReadOnlyList<TypeDefinitionAst>? classes)
        {
            return null;
        }

        private static string? ResolveStaticMethodReturnType(ITypeName typeName, string methodName)
        {
            try
            {
                Type? type = typeName.GetReflectionType();
                if (type is null)
                {
                    return null;
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (MethodInfo method in methods)
                {
                    if (method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    {
                        return method.ReturnType.FullName!;
                    }
                }
            }
            catch
            {
                // Reflection failures are non-fatal
            }

            return null;
        }

        internal static string? ResolveTypeName(ITypeName typeName)
        {
            try
            {
                Type? type = typeName.GetReflectionType();
                if (type is not null)
                {
                    return type.FullName!;
                }
            }
            catch
            {
                // Reflection failures are non-fatal
            }

            return typeName.FullName!;
        }

        private static Dictionary<string, string> BuildAssignmentMap(
            FunctionDefinitionAst func,
            IReadOnlyList<TypeDefinitionAst>? classes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Ast node in func.Body.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)node;
                if (assignment.Left is not VariableExpressionAst varExpr)
                {
                    continue;
                }

                string varName = varExpr.VariablePath.UserPath;

                if (assignment.Right is StatementAst rightStmt)
                {
                    string? type = InferStatementType(rightStmt, map, classes);
                    if (type is not null)
                    {
                        map[varName] = type;
                    }
                }
            }

            return map;
        }

        private static Dictionary<string, string> BuildClassMethodAssignmentMap(
            FunctionMemberAst method,
            TypeDefinitionAst containingClass,
            IReadOnlyList<TypeDefinitionAst> classes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            map["this"] = containingClass.Name;

            foreach (Ast node in method.Body.FindAll(static a => a is AssignmentStatementAst, searchNestedScriptBlocks: false))
            {
                var assignment = (AssignmentStatementAst)node;
                if (assignment.Left is not VariableExpressionAst varExpr)
                {
                    continue;
                }

                string varName = varExpr.VariablePath.UserPath;

                if (assignment.Right is StatementAst rightStmt)
                {
                    string? type = InferStatementType(rightStmt, map, classes);
                    if (type is not null)
                    {
                        map[varName] = type;
                    }
                }
            }

            return map;
        }
    }
}
