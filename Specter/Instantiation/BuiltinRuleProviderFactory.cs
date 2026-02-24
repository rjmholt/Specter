using Specter.Configuration;
using Specter.Rules.Builtin;
using System.Collections.Generic;

namespace Specter.Instantiation
{
    internal class BuiltinRuleProviderFactory : DeclarativeRuleProviderFactory
    {
        internal BuiltinRuleProviderFactory(
            IReadOnlyDictionary<string, IRuleConfiguration?>? ruleConfigurationCollection)
            : base(Default.RuleConfiguration, BuiltinRules.DefaultRules, ruleConfigurationCollection)
        {
        }
    }
}
