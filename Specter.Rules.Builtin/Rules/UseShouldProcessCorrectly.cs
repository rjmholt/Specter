using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("ShouldProcess", typeof(Strings), nameof(Strings.ShouldProcessDescription))]
    internal class UseShouldProcessCorrectly : ScriptRule
    {
        private static readonly HashSet<string> s_cmdletsWithShouldProcess = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Remove-Item", "Move-Item", "Copy-Item", "Rename-Item", "Set-Item",
            "Clear-Item", "New-Item", "Set-ItemProperty", "Remove-ItemProperty",
            "Clear-ItemProperty", "Rename-ItemProperty", "Move-ItemProperty",
            "Copy-ItemProperty", "New-ItemProperty", "Set-Content", "Clear-Content",
            "Add-Content", "Stop-Process", "Set-Service", "Stop-Service",
            "Start-Service", "Restart-Service", "Suspend-Service", "Resume-Service",
            "Install-Module", "Uninstall-Module", "Update-Module", "Save-Module",
            "Install-Script", "Uninstall-Script", "Update-Script",
            "Register-PSRepository", "Set-PSRepository", "Unregister-PSRepository",
            "Install-Package", "Uninstall-Package",
            "New-PSDrive", "Remove-PSDrive",
        };

        internal UseShouldProcessCorrectly(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var allFunctions = ast.FindAll(static a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true)
                .Cast<FunctionDefinitionAst>()
                .ToList();

            var callGraph = BuildCallGraph(allFunctions);

            foreach (FunctionDefinitionAst funcAst in allFunctions)
            {
                bool declaresShouldProcess = DeclaresSupportsShouldProcess(funcAst);
                bool callsShouldProcess = CallsShouldProcessOrShouldContinue(funcAst);

                if (declaresShouldProcess && !callsShouldProcess)
                {
                    bool reachesShouldProcess = ReachesShouldProcessTransitively(funcAst, allFunctions, callGraph);
                    bool callsBuiltinWithShouldProcess = CallsBuiltinWithShouldProcess(funcAst);

                    if (!reachesShouldProcess && !callsBuiltinWithShouldProcess)
                    {
                        IScriptExtent extent = GetShouldProcessAttributeExtent(funcAst) ?? funcAst.Extent;
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.ShouldProcessErrorHasAttribute, funcAst.Name),
                            extent);
                    }
                }
                else if (!declaresShouldProcess && callsShouldProcess)
                {
                    if (!IsPhysicallyNestedInShouldProcessFunction(funcAst))
                    {
                        IScriptExtent extent = GetShouldProcessCallExtent(funcAst) ?? funcAst.Extent;
                        yield return CreateDiagnostic(
                            string.Format(CultureInfo.CurrentCulture, Strings.ShouldProcessErrorHasCmdlet, funcAst.Name),
                            extent);
                    }
                }
            }
        }

        private static Dictionary<string, HashSet<string>> BuildCallGraph(IReadOnlyList<FunctionDefinitionAst> functions)
        {
            var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (FunctionDefinitionAst funcAst in functions)
            {
                var callees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Ast node in funcAst.Body.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: false))
                {
                    string name = ((CommandAst)node).GetCommandName();
                    if (name is not null)
                    {
                        callees.Add(name);
                    }
                }

                graph[funcAst.Name] = callees;
            }

            return graph;
        }

        private static bool ReachesShouldProcessTransitively(
            FunctionDefinitionAst funcAst,
            IReadOnlyList<FunctionDefinitionAst> allFunctions,
            Dictionary<string, HashSet<string>> callGraph)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(funcAst.Name);
            visited.Add(funcAst.Name);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!callGraph.TryGetValue(current, out HashSet<string>? callees) || callees is null)
                {
                    continue;
                }

                foreach (string callee in callees)
                {
                    if (callee is null || !visited.Add(callee))
                    {
                        continue;
                    }

                    FunctionDefinitionAst? calleeFuncAst = allFunctions.FirstOrDefault(
                        f => string.Equals(f.Name, callee, StringComparison.OrdinalIgnoreCase));

                    if (calleeFuncAst is not null)
                    {
                        if (CallsShouldProcessOrShouldContinue(calleeFuncAst))
                        {
                            return true;
                        }

                        queue.Enqueue(callee);
                    }
                }
            }

            return false;
        }

        private static bool CallsBuiltinWithShouldProcess(FunctionDefinitionAst funcAst)
        {
            foreach (Ast node in funcAst.Body.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: false))
            {
                string name = ((CommandAst)node).GetCommandName();
                if (name is not null && s_cmdletsWithShouldProcess.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPhysicallyNestedInShouldProcessFunction(FunctionDefinitionAst funcAst)
        {
            Ast parent = funcAst.Parent;
            while (parent is not null)
            {
                if (parent is FunctionDefinitionAst parentFunc && DeclaresSupportsShouldProcess(parentFunc))
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static bool DeclaresSupportsShouldProcess(FunctionDefinitionAst funcAst)
        {
            if (funcAst.Body?.ParamBlock?.Attributes is null)
            {
                return false;
            }

            foreach (AttributeAst attr in funcAst.Body.ParamBlock.Attributes)
            {
                if (!IsCmdletBindingAttribute(attr))
                {
                    continue;
                }

                foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
                {
                    if (string.Equals(namedArg.ArgumentName, "SupportsShouldProcess", StringComparison.OrdinalIgnoreCase))
                    {
                        if (namedArg.ExpressionOmitted)
                        {
                            return true;
                        }

                        object? value = namedArg.GetValue();
                        return AstTools.IsTrue(value);
                    }
                }
            }

            return false;
        }

        private static IScriptExtent? GetShouldProcessAttributeExtent(FunctionDefinitionAst funcAst)
        {
            if (funcAst.Body?.ParamBlock?.Attributes is null)
            {
                return null;
            }

            foreach (AttributeAst attr in funcAst.Body.ParamBlock.Attributes)
            {
                if (IsCmdletBindingAttribute(attr))
                {
                    foreach (NamedAttributeArgumentAst namedArg in attr.NamedArguments)
                    {
                        if (string.Equals(namedArg.ArgumentName, "SupportsShouldProcess", StringComparison.OrdinalIgnoreCase))
                        {
                            return namedArg.Extent;
                        }
                    }
                }
            }

            return null;
        }

        private static IScriptExtent? GetShouldProcessCallExtent(FunctionDefinitionAst funcAst)
        {
            foreach (Ast node in funcAst.Body.FindAll(static a => a is InvokeMemberExpressionAst, searchNestedScriptBlocks: false))
            {
                var invokeAst = (InvokeMemberExpressionAst)node;
                if (invokeAst.Member is StringConstantExpressionAst memberName
                    && (string.Equals(memberName.Value, "ShouldProcess", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(memberName.Value, "ShouldContinue", StringComparison.OrdinalIgnoreCase)))
                {
                    return memberName.Extent;
                }
            }

            return null;
        }

        private static bool IsCmdletBindingAttribute(AttributeAst attr)
        {
            return attr.TypeName.GetReflectionAttributeType() == typeof(CmdletBindingAttribute);
        }

        private static bool CallsShouldProcessOrShouldContinue(FunctionDefinitionAst funcAst)
        {
            foreach (Ast node in funcAst.Body.FindAll(static a => a is InvokeMemberExpressionAst, searchNestedScriptBlocks: false))
            {
                var invokeAst = (InvokeMemberExpressionAst)node;
                if (invokeAst.Member is StringConstantExpressionAst memberName
                    && (string.Equals(memberName.Value, "ShouldProcess", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(memberName.Value, "ShouldContinue", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
