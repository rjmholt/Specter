using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Specter.CommandDatabase;
using Specter.Rules;

namespace Specter.Rules.Builtin.Rules
{
    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseCmdletCorrectly", typeof(Strings), nameof(Strings.UseCmdletCorrectlyDescription))]
    internal class UseCmdletCorrectly : ScriptRule
    {
        private readonly IPowerShellCommandDatabase _commandDb;

        internal UseCmdletCorrectly(RuleInfo ruleInfo, IPowerShellCommandDatabase commandDb)
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

            foreach (Ast node in ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)node;
                string? commandName = cmdAst.GetCommandName();

                if (commandName is null)
                {
                    continue;
                }

                if (HasSplattedArgument(cmdAst))
                {
                    continue;
                }

                if (IsPipedTo(cmdAst))
                {
                    continue;
                }

                if (!_commandDb.TryGetCommand(commandName, platforms: null, out CommandMetadata? meta) || meta is null)
                {
                    continue;
                }

                if (!string.Equals(meta!.CommandType, "Cmdlet", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(meta.CommandType, "Function", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (meta.Parameters is null || meta.Parameters.Count == 0)
                {
                    continue;
                }

                var suppliedParams = GetSuppliedParameterNames(cmdAst);
                bool hasPositionalArgs = HasPositionalArguments(cmdAst);

                if (hasPositionalArgs)
                {
                    continue;
                }

                if (!HasUnsatisfiedMandatoryParameter(meta, suppliedParams))
                {
                    continue;
                }

                yield return CreateDiagnostic(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UseCmdletCorrectlyError,
                        commandName),
                    cmdAst.CommandElements[0]);
            }
        }

        private static bool HasUnsatisfiedMandatoryParameter(CommandMetadata meta, HashSet<string> suppliedParams)
        {
            var paramSetMandatories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (ParameterMetadata param in meta.Parameters)
            {
                foreach (ParameterSetInfo setInfo in param.ParameterSets)
                {
                    string setName = setInfo.SetName ?? "__AllParameterSets";
                    if (!paramSetMandatories.TryGetValue(setName, out List<string>? mandatoryList))
                    {
                        mandatoryList = new List<string>();
                        paramSetMandatories[setName] = mandatoryList;
                    }

                    if (setInfo.IsMandatory)
                    {
                        mandatoryList.Add(param.Name);
                    }
                }
            }

            if (paramSetMandatories.Count == 0)
            {
                return false;
            }

            foreach (var kvp in paramSetMandatories)
            {
                bool allSatisfied = true;
                foreach (string mandatory in kvp.Value)
                {
                    if (!suppliedParams.Contains(mandatory))
                    {
                        allSatisfied = false;
                        break;
                    }
                }

                if (allSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSplattedArgument(CommandAst cmdAst)
        {
            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is VariableExpressionAst varExpr && varExpr.Splatted)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPipedTo(CommandAst cmdAst)
        {
            if (cmdAst.Parent is PipelineAst pipeline && pipeline.PipelineElements.Count > 1)
            {
                return !ReferenceEquals(pipeline.PipelineElements[0], cmdAst);
            }

            return false;
        }

        private static HashSet<string> GetSuppliedParameterNames(CommandAst cmdAst)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (element is CommandParameterAst paramAst)
                {
                    names.Add(paramAst.ParameterName);
                }
            }

            return names;
        }

        private static bool HasPositionalArguments(CommandAst cmdAst)
        {
            bool pastFirstElement = false;
            bool lastWasParam = false;

            foreach (CommandElementAst element in cmdAst.CommandElements)
            {
                if (!pastFirstElement)
                {
                    pastFirstElement = true;
                    continue;
                }

                if (element is CommandParameterAst paramAst)
                {
                    lastWasParam = paramAst.Argument is null;
                    continue;
                }

                if (!lastWasParam)
                {
                    return true;
                }

                lastWasParam = false;
            }

            return false;
        }
    }
}
