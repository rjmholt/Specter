using PSpecter.Rules;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSpecter.Instantiation
{
    public interface IRuleProvider
    {
        IEnumerable<RuleInfo> GetRuleInfos();

        IEnumerable<ScriptRule> GetScriptRules();

        void ReturnRule(Rule rule);
    }
}
