using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Specter.Logging;
using Specter.Rules;

namespace Specter.Instantiation
{
    internal static class PSRuleDiscovery
    {
        /// <summary>
        /// Discovers rule functions from a runspace using both native Specter attribute
        /// convention and PSSA legacy Measure-* convention.
        /// </summary>
        internal static List<DiscoveredPSRule> DiscoverRules(
            Runspace runspace,
            IAnalysisLogger? logger)
        {
            var results = new List<DiscoveredPSRule>();

            using var ps = PowerShell.Create();
            ps.Runspace = runspace;

            ps.AddCommand("Get-Command")
                .AddParameter("CommandType", CommandTypes.Function);

            var functions = ps.Invoke<FunctionInfo>();

            foreach (FunctionInfo function in functions)
            {
                DiscoveredPSRule? rule = TryDiscoverNative(function, logger);
                if (rule is not null)
                {
                    results.Add(rule);
                    continue;
                }

                rule = TryDiscoverLegacy(function, logger);
                if (rule is not null)
                {
                    results.Add(rule);
                }
            }

            return results;
        }

        /// <summary>
        /// Native discovery: functions with [SpecterRule] attribute.
        /// </summary>
        private static DiscoveredPSRule? TryDiscoverNative(FunctionInfo function, IAnalysisLogger? logger)
        {
            if (function.ScriptBlock?.Attributes is null)
            {
                return null;
            }

            foreach (Attribute attr in function.ScriptBlock.Attributes)
            {
                if (attr is not SpecterRuleAttribute specterAttr)
                {
                    continue;
                }

                if (!RuleInfo.TryGetFromFunctionInfo(function, out RuleInfo? ruleInfo) || ruleInfo is null)
                {
                    logger?.Warning($"Function '{function.Name}' has [SpecterRule] but could not produce RuleInfo. Skipping.");
                    return null;
                }

                logger?.Debug($"Discovered native Specter rule: {ruleInfo.FullName} from function '{function.Name}'.");
                return new DiscoveredPSRule(ruleInfo, function.Name, PSRuleConvention.Native);
            }

            return null;
        }

        /// <summary>
        /// Legacy PSSA discovery: functions named Measure-* with a ScriptBlockAst parameter.
        /// Accepts parameters named "ScriptBlockAst" (standard convention) or any parameter
        /// whose type is ScriptBlockAst (PSSA sample rule convention).
        /// </summary>
        private static DiscoveredPSRule? TryDiscoverLegacy(FunctionInfo function, IAnalysisLogger? logger)
        {
            if (!function.Name.StartsWith("Measure-", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            bool hasScriptBlockAstParam = false;
            foreach (var param in function.Parameters)
            {
                if (param.Key.Equals("ScriptBlockAst", StringComparison.OrdinalIgnoreCase))
                {
                    hasScriptBlockAstParam = true;
                    break;
                }

                if (param.Value.ParameterType == typeof(System.Management.Automation.Language.ScriptBlockAst))
                {
                    hasScriptBlockAstParam = true;
                    break;
                }
            }

            if (!hasScriptBlockAstParam)
            {
                return null;
            }

            string ruleName = function.Name.Substring("Measure-".Length);
            string moduleNamespace = function.ModuleName ?? "CustomRule";

            var ruleInfo = new LegacyRuleInfoBuilder(ruleName, moduleNamespace).Build();

            logger?.Debug($"Discovered legacy PSSA rule: {ruleInfo.FullName} from function '{function.Name}'.");
            return new DiscoveredPSRule(ruleInfo, function.Name, PSRuleConvention.PssaLegacy);
        }
    }

    internal enum PSRuleConvention
    {
        Native,
        PssaLegacy,
    }

    internal sealed class DiscoveredPSRule
    {
        internal DiscoveredPSRule(RuleInfo ruleInfo, string functionName, PSRuleConvention convention)
        {
            RuleInfo = ruleInfo;
            FunctionName = functionName;
            Convention = convention;
        }

        internal RuleInfo RuleInfo { get; }
        internal string FunctionName { get; }
        internal PSRuleConvention Convention { get; }
    }

    /// <summary>
    /// Builds a RuleInfo for PSSA legacy Measure-* functions that lack a [Rule] attribute.
    /// Uses reflection to access the private constructor.
    /// </summary>
    internal sealed class LegacyRuleInfoBuilder
    {
        private readonly string _name;
        private readonly string _namespace;

        internal LegacyRuleInfoBuilder(string name, string @namespace)
        {
            _name = name;
            _namespace = @namespace;
        }

        internal RuleInfo Build()
        {
            var ctor = typeof(RuleInfo).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string), typeof(string) },
                modifiers: null);

            if (ctor is null)
            {
                throw new InvalidOperationException("Cannot find RuleInfo(string, string) constructor.");
            }

            var ruleInfo = (RuleInfo)ctor.Invoke(new object[] { _name, _namespace });
            return ruleInfo;
        }
    }
}
