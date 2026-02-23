using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Text;

namespace Specter.Instantiation
{
    public interface IRuleProvider
    {
        IEnumerable<RuleInfo> GetRuleInfos();

        IEnumerable<ScriptRule> GetScriptRules();

        void ReturnRule(Rule rule);
    }
}
