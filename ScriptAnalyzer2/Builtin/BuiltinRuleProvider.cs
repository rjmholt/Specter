using Microsoft.PowerShell.ScriptAnalyzer.Builder;
using Microsoft.PowerShell.ScriptAnalyzer.Builtin.Rules;
using Microsoft.PowerShell.ScriptAnalyzer.Configuration;
using Microsoft.PowerShell.ScriptAnalyzer.Execution;
using Microsoft.PowerShell.ScriptAnalyzer.Runtime;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.ScriptAnalyzer.Builtin
{
    public static class BuiltinRules
    {
        public static IReadOnlyList<Type> DefaultRules { get; } = new[]
        {
            typeof(AvoidDefaultTrueValueSwitchParameter),
            typeof(AvoidDefaultValueForMandatoryParameter),
            typeof(AvoidEmptyCatchBlock),
            typeof(AvoidGlobalVars),
            typeof(AvoidNullOrEmptyHelpMessageAttribute),
            typeof(AvoidPositionalParameters),
            typeof(AvoidTrailingWhitespace),
            typeof(AvoidUsingCmdletAliases),
            typeof(AvoidUsingConvertToSecureStringWithPlainText),
            typeof(AvoidUsingInvokeExpression),
            typeof(AvoidUsingWMICmdlet),
            typeof(AvoidUsingWriteHost),
            typeof(MisleadingBacktick),
            typeof(PossibleIncorrectComparisonWithNull),
            typeof(UseDeclaredVarsMoreThanAssignments),
            typeof(UseShouldProcessForStateChangingFunctions),
        };
    }

    public static class Default
    {
        private static readonly Lazy<RuleComponentProvider> s_ruleComponentProviderLazy = new Lazy<RuleComponentProvider>(BuildRuleComponentProvider);

        public static IReadOnlyDictionary<string, IRuleConfiguration> RuleConfiguration { get; } = new Dictionary<string, IRuleConfiguration>(StringComparer.OrdinalIgnoreCase)
        {
            { "PS/AvoidDefaultValueForMandatoryParameter", null },
            { "PS/AvoidDefaultValueSwitchParameter", null },
            { "PS/AvoidNullOrEmptyHelpMessageAttribute", null },
            { "PS/AvoidTrailingWhitespace", null },
            { "PS/AvoidUsingCmdletAliases", null },
            { "PS/AvoidUsingConvertToSecureStringWithPlainText", null },
            { "PS/AvoidUsingEmptyCatchBlock", null },
            { "PS/AvoidUsingInvokeExpression", null },
            { "PS/AvoidUsingWriteHost", null },
            { "PS/AvoidGlobalVars", null },
            { "PS/AvoidUsingPositionalParameters", null },
            { "PS/AvoidUsingWMICmdlet", null },
            { "PS/MisleadingBacktick", null },
            { "PS/PossibleIncorrectComparisonWithNull", null },
            { "PS/UseDeclaredVarsMoreThanAssignments", null },
            { "PS/UseShouldProcessForStateChangingFunctions", null },
        };

        public static IRuleExecutorFactory RuleExecutorFactory { get; } = new ParallelLinqRuleExecutorFactory();

        public static RuleComponentProvider RuleComponentProvider => s_ruleComponentProviderLazy.Value;

        private static RuleComponentProvider BuildRuleComponentProvider()
        {
            return new RuleComponentProviderBuilder()
                .AddSingleton<IPowerShellCommandDatabase>(BuiltinCommandDatabase.Instance)
                .Build();
        }
    }

}
