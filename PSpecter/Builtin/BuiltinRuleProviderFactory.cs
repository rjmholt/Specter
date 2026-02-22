#nullable disable

﻿using PSpecter.Builder;
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
        public BuiltinRuleProviderFactory(
            IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection)
            : base(ruleConfigurationCollection ?? Default.RuleConfiguration, BuiltinRules.DefaultRules.ToDictionary(t => t, _ => (RuleInfo)null))
        {
        }
    }
}
