using Specter.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace Specter.Instantiation
{
    public interface IRuleProviderFactory
    {
        IRuleProvider CreateRuleProvider(RuleComponentProvider ruleComponentProvider);
    }
}
