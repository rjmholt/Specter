using Specter.Configuration;
using Specter.Rules;
using System;
using System.Collections.Generic;

namespace Specter.Instantiation
{
    /// <summary>
    /// Base factory for declarative rule catalogs.
    /// Subclasses only provide rule types and default configuration.
    /// </summary>
    public abstract class DeclarativeRuleProviderFactory : TypeRuleProviderFactory
    {
        protected DeclarativeRuleProviderFactory(
            IReadOnlyDictionary<string, IRuleConfiguration?> defaultRuleConfiguration,
            IReadOnlyList<Type> ruleTypes,
            IReadOnlyDictionary<string, IRuleConfiguration?>? ruleConfigurationOverride)
            : base(
                ruleConfigurationOverride ?? defaultRuleConfiguration,
                ToRuleInfoMap(ruleTypes))
        {
        }

        private static IReadOnlyDictionary<Type, RuleInfo?> ToRuleInfoMap(IReadOnlyList<Type> ruleTypes)
        {
            var map = new Dictionary<Type, RuleInfo?>(ruleTypes.Count);
            for (int i = 0; i < ruleTypes.Count; i++)
            {
                map[ruleTypes[i]] = null;
            }

            return map;
        }
    }
}
