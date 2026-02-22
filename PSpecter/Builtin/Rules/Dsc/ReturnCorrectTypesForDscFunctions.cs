using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("ReturnCorrectTypesForDSCFunctions", typeof(Strings), nameof(Strings.ReturnCorrectTypesForDSCFunctionsDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Information)]
    public class ReturnCorrectTypesForDscFunctions : ScriptRule
    {
        private static readonly Dictionary<string, string> s_resourceFunctionExpectedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Get-TargetResource"] = "System.Collections.Hashtable",
            ["Test-TargetResource"] = "System.Boolean",
        };

        public ReturnCorrectTypesForDscFunctions(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            foreach (ScriptDiagnostic diag in AnalyzeResourceFunctions(ast))
            {
                yield return diag;
            }

            foreach (ScriptDiagnostic diag in AnalyzeDscClasses(ast))
            {
                yield return diag;
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeResourceFunctions(Ast ast)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);
            IReadOnlyList<TypeDefinitionAst> classes = DscResourceHelper.GetDscClasses(ast);

            foreach (FunctionDefinitionAst func in dscFuncs)
            {
                IReadOnlyList<OutputInfo> outputs = PipelineOutputAnalyzer.GetOutputs(func, classes);

                if (func.Name.Equals("Set-TargetResource", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (OutputInfo output in outputs)
                    {
                        yield return CreateDiagnostic(
                            Strings.ReturnCorrectTypesForSetTargetResourceFunctionsDSCError,
                            output.Extent);
                    }
                }
                else if (s_resourceFunctionExpectedTypes.TryGetValue(func.Name, out string? expectedType))
                {
                    foreach (OutputInfo output in outputs)
                    {
                        if (PipelineOutputAnalyzer.ShouldSkipType(output.TypeName, expectedType))
                        {
                            continue;
                        }

                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.ReturnCorrectTypesForGetTestTargetResourceFunctionsDSCResourceError,
                                func.Name,
                                expectedType,
                                output.TypeName),
                            output.Extent);
                    }
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> AnalyzeDscClasses(Ast ast)
        {
            IReadOnlyList<TypeDefinitionAst> dscClasses = DscResourceHelper.GetDscClasses(ast);

            foreach (TypeDefinitionAst dscClass in dscClasses)
            {
                foreach (MemberAst member in dscClass.Members)
                {
                    if (member is not FunctionMemberAst method)
                    {
                        continue;
                    }

                    string? methodName = null;
                    foreach (string name in DscResourceHelper.ClassResourceMethodNames)
                    {
                        if (method.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            methodName = name;
                            break;
                        }
                    }

                    if (methodName is null)
                    {
                        continue;
                    }

                    if (methodName.Equals("Set", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (ScriptDiagnostic diag in CheckSetMethodReturns(method, dscClass))
                        {
                            yield return diag;
                        }
                    }
                    else
                    {
                        string expectedType = GetExpectedClassReturnType(methodName, dscClass);
                        foreach (ScriptDiagnostic diag in CheckClassMethodReturns(method, dscClass, expectedType, dscClasses))
                        {
                            yield return diag;
                        }
                    }
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> CheckSetMethodReturns(FunctionMemberAst method, TypeDefinitionAst dscClass)
        {
            foreach (Ast node in method.Body.FindAll(a => a is ReturnStatementAst, searchNestedScriptBlocks: false))
            {
                var ret = (ReturnStatementAst)node;
                if (ret.Pipeline is not null)
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ReturnCorrectTypesForSetFunctionsDSCError,
                            dscClass.Name),
                        ret.Extent);
                }
            }
        }

        private IEnumerable<ScriptDiagnostic> CheckClassMethodReturns(
            FunctionMemberAst method,
            TypeDefinitionAst dscClass,
            string expectedType,
            IReadOnlyList<TypeDefinitionAst> classes)
        {
            IReadOnlyList<OutputInfo> returns = PipelineOutputAnalyzer.GetReturnStatementOutputs(method, dscClass, classes);

            foreach (OutputInfo ret in returns)
            {
                if (ret.TypeName is null)
                {
                    yield return CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.ReturnCorrectTypesForDSCFunctionsNoTypeError,
                            method.Name,
                            dscClass.Name,
                            expectedType),
                        ret.Extent);
                    continue;
                }

                if (PipelineOutputAnalyzer.ShouldSkipType(ret.TypeName, expectedType))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ReturnCorrectTypesForDSCFunctionsWrongTypeError,
                        method.Name,
                        dscClass.Name,
                        expectedType,
                        ret.TypeName),
                    ret.Extent);
            }
        }

        private static string GetExpectedClassReturnType(string methodName, TypeDefinitionAst dscClass)
        {
            if (methodName.Equals("Test", StringComparison.OrdinalIgnoreCase))
            {
                return "System.Boolean";
            }

            return dscClass.Name;
        }
    }
}
