using Specter.Configuration;
using Specter.Instantiation;
using Specter.Rules.Builtin;
using System;
using System.Collections.Generic;

namespace Specter.Builder
{
    public class BuiltinRulesBuilder
    {
        private IReadOnlyDictionary<string, IRuleConfiguration?>? _ruleConfiguration;

        private RuleComponentProvider? _ruleComponents;

        public BuiltinRulesBuilder WithRuleConfiguration(IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurationCollection)
        {
            _ruleConfiguration = ruleConfigurationCollection;
            return this;
        }

        public BuiltinRulesBuilder WithRuleComponentProvider(RuleComponentProvider ruleComponentProvider)
        {
            _ruleComponents = ruleComponentProvider;
            return this;
        }

        public BuiltinRulesBuilder WithRuleComponentBuilder(Action<RuleComponentProviderBuilder> configureRuleComponents)
        {
            var ruleComponentProviderBuilder = new RuleComponentProviderBuilder();
            configureRuleComponents(ruleComponentProviderBuilder);
            _ruleComponents = ruleComponentProviderBuilder.Build();
            return this;
        }

        public IRuleProviderFactory Build()
        {
            return new BuiltinRuleProviderFactory(_ruleConfiguration ?? Default.RuleConfiguration);
        }
    }
}
