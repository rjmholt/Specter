using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules.Dsc
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseIdenticalParametersForDSC", typeof(Strings), nameof(Strings.UseIdenticalParametersDSCDescription), Namespace = "PSDSC", Severity = DiagnosticSeverity.Error)]
    internal class UseIdenticalParametersDsc : ScriptRule
    {
        public UseIdenticalParametersDsc(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            IReadOnlyList<FunctionDefinitionAst> dscFuncs = DscResourceHelper.GetDscResourceFunctions(ast);

            FunctionDefinitionAst? setFunc = null;
            FunctionDefinitionAst? testFunc = null;

            foreach (FunctionDefinitionAst func in dscFuncs)
            {
                if (func.Name.Equals("Set-TargetResource", StringComparison.OrdinalIgnoreCase))
                {
                    setFunc = func;
                }
                else if (func.Name.Equals("Test-TargetResource", StringComparison.OrdinalIgnoreCase))
                {
                    testFunc = func;
                }
            }

            if (setFunc is null || testFunc is null)
            {
                yield break;
            }

            IReadOnlyList<ParameterAst> setParams = GetParameters(setFunc);
            IReadOnlyList<ParameterAst> testParams = GetParameters(testFunc);

            if (setParams.Count != testParams.Count)
            {
                yield return CreateDiagnostic(
                    Strings.UseIdenticalParametersDSCError,
                    setFunc.Extent);
                yield break;
            }

            var setParamDict = new Dictionary<string, ParameterAst>(StringComparer.OrdinalIgnoreCase);
            foreach (ParameterAst p in setParams)
            {
                setParamDict[p.Name.VariablePath.UserPath] = p;
            }

            foreach (ParameterAst testParam in testParams)
            {
                string paramName = testParam.Name.VariablePath.UserPath;

                if (!setParamDict.TryGetValue(paramName, out ParameterAst? setParam)
                    || setParam is null
                    || !CompareParameters(setParam, testParam))
                {
                    yield return CreateDiagnostic(
                        Strings.UseIdenticalParametersDSCError,
                        testParam.Extent);
                }
            }
        }

        private static IReadOnlyList<ParameterAst> GetParameters(FunctionDefinitionAst func)
        {
            if (func.Body?.ParamBlock?.Parameters is not null)
            {
                return func.Body.ParamBlock.Parameters;
            }

            if (func.Parameters is not null)
            {
                return func.Parameters;
            }

            return Array.Empty<ParameterAst>();
        }

        private static bool CompareParameters(ParameterAst param1, ParameterAst param2)
        {
            if (param1.StaticType != param2.StaticType)
            {
                return false;
            }

            IReadOnlyList<AttributeBaseAst> attrs1 = param1.Attributes;
            IReadOnlyList<AttributeBaseAst> attrs2 = param2.Attributes;

            if (attrs1.Count == 0 && attrs2.Count == 0)
            {
                return true;
            }

            if (attrs1.Count == 0 || attrs2.Count == 0)
            {
                return false;
            }

            var attrNames1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AttributeBaseAst a in attrs1)
            {
                attrNames1.Add(a.TypeName.Name);
            }

            var attrNames2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AttributeBaseAst a in attrs2)
            {
                attrNames2.Add(a.TypeName.Name);
            }

            return attrNames1.SetEquals(attrNames2);
        }
    }
}
