using PSpecter.Builder;
using PSpecter.Configuration;
using PSpecter.Instantiation;
using PSpecter.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PSpecter.Builtin
{
    internal class BuiltinRuleProviderFactory : TypeRuleProviderFactory
    {
        internal BuiltinRuleProviderFactory(
            IReadOnlyDictionary<string, IRuleConfiguration>? ruleConfigurationCollection)
            : base(
                ruleConfigurationCollection ?? (IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration,
                BuiltinRules.DefaultRules.ToDictionary(t => t, _ => (RuleInfo?)null))
        {
        }
    }
}
