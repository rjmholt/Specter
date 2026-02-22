using PSpecter.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSpecter.Instantiation
{
    public interface IRuleProviderFactory
    {
        IRuleProvider CreateRuleProvider(RuleComponentProvider ruleComponentProvider);
    }
}
