using PSpecter.Builder;
using PSpecter.Configuration;
using PSpecter.Rules;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PSpecter.Instantiation
{
    internal static class RuleGeneration
    {
        public static bool TryGetRuleFromType(
            IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection,
            RuleComponentProvider ruleComponentProvider,
            Type type,
            ref RuleInfo ruleInfo,
            out TypeRuleFactory<ScriptRule> ruleFactory)
        {
            ruleFactory = null;
            if (ruleInfo == null
                && !RuleInfo.TryGetFromRuleType(type, out ruleInfo))
            {
                return false;
            }

            return typeof(ScriptRule).IsAssignableFrom(type)
                && TryGetRuleFactory(ruleInfo, type, ruleConfigurationCollection, ruleComponentProvider, out ruleFactory);
        }

        private static bool TryGetRuleFactory<TRuleBase>(
            RuleInfo ruleInfo,
            Type ruleType,
            IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection,
            RuleComponentProvider ruleComponentProvider,
            out TypeRuleFactory<TRuleBase> factory)
        {
            ConstructorInfo[] ruleConstructors = ruleType.GetConstructors();
            if (ruleConstructors.Length != 1)
            {
                factory = null;
                return false;
            }
            ConstructorInfo ruleConstructor = ruleConstructors[0];

            ruleConfigurationCollection.TryGetValue(ruleInfo.FullName, out IRuleConfiguration ruleConfiguration);
            bool isEnabled = ruleConfiguration?.Common?.Enabled ?? true;

            if (ruleInfo.IsIdempotent)
            {
                factory = new ConstructorInjectionIdempotentRuleFactory<TRuleBase>(
                    ruleComponentProvider,
                    ruleInfo,
                    ruleConstructor,
                    ruleConfiguration,
                    isEnabled);
                return true;
            }

            if (typeof(IResettable).IsAssignableFrom(ruleType))
            {
                factory = new ConstructorInjectingResettableRulePoolingFactory<TRuleBase>(
                    ruleComponentProvider,
                    ruleInfo,
                    ruleConstructor,
                    ruleConfiguration,
                    isEnabled);
                return true;
            }

            if (typeof(IDisposable).IsAssignableFrom(ruleType))
            {
                factory = new ConstructorInjectingDisposableRuleFactory<TRuleBase>(
                    ruleComponentProvider,
                    ruleInfo,
                    ruleConstructor,
                    ruleConfiguration,
                    isEnabled);
                return true;
            }

            factory = new ConstructorInjectingRuleFactory<TRuleBase>(
                ruleComponentProvider,
                ruleInfo,
                ruleConstructor,
                ruleConfiguration,
                isEnabled);
            return true;
        }
    }
}
