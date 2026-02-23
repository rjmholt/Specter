using Specter.Builder;
using Specter.Configuration;
using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Specter.Instantiation
{
    public class TypeRuleProvider : IRuleProvider
    {

        private readonly IReadOnlyDictionary<RuleInfo, TypeRuleFactory<ScriptRule>> _scriptRuleFactories;

        internal TypeRuleProvider(
            IReadOnlyDictionary<RuleInfo, TypeRuleFactory<ScriptRule>> scriptRuleFactories)
        {
            _scriptRuleFactories = scriptRuleFactories;
        }

        public IEnumerable<RuleInfo> GetRuleInfos()
        {
            return _scriptRuleFactories.Keys;
        }

        public IEnumerable<ScriptRule> GetScriptRules()
        {
            foreach (TypeRuleFactory<ScriptRule> ruleFactory in _scriptRuleFactories.Values)
            {
                if (!ruleFactory.IsEnabled)
                {
                    continue;
                }

                yield return ruleFactory.GetRuleInstance();
            }
        }

        public void ReturnRule(Rule rule)
        {
            if (!(rule is ScriptRule scriptRule))
            {
                return;
            }

            if (_scriptRuleFactories.TryGetValue(rule.RuleInfo, out TypeRuleFactory<ScriptRule>? astRuleFactory))
            {
                astRuleFactory.ReturnRuleInstance(scriptRule);
            }
        }

    }
}
