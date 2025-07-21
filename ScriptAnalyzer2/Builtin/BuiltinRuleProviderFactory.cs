using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Configuration;
using Microsoft.PowerShell.ScriptAnalyzer.Instantiation;
using Microsoft.PowerShell.ScriptAnalyzer.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin
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
