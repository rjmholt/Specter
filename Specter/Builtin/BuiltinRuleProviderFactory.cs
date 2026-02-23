using Specter.Builder;
using Specter.Configuration;
using Specter.Instantiation;
using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Specter.Builtin
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
