using PSpecter.Builder;
using PSpecter.Builtin.Editors;
using PSpecter.Builtin.Rules;
using PSpecter.Configuration;
using PSpecter.Execution;
using PSpecter.Formatting;
using PSpecter.CommandDatabase;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules;
using System;
using System.Collections.Generic;

namespace PSpecter.Builtin
{
    public static class BuiltinRules
    {
        public static IReadOnlyList<Type> DefaultRules { get; } = new[]
        {
            typeof(AlignAssignmentStatement),
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
            typeof(PlaceCloseBrace),
            typeof(PlaceOpenBrace),
            typeof(PossibleIncorrectComparisonWithNull),
            typeof(UseConsistentIndentation),
            typeof(UseConsistentWhitespace),
            typeof(UseCorrectCasing),
            typeof(UseDeclaredVarsMoreThanAssignments),
            typeof(UseShouldProcessForStateChangingFunctions),
        };
    }

    public static class Default
    {
        private static readonly Lazy<RuleComponentProvider> s_ruleComponentProviderLazy = new Lazy<RuleComponentProvider>(BuildRuleComponentProvider);

        private static IEditorConfiguration DisabledEditorConfig<T>() where T : IEditorConfiguration, new()
        {
            var config = new T();
            config.Common.Enabled = false;
            return config;
        }

        public static IReadOnlyDictionary<string, IRuleConfiguration> RuleConfiguration { get; } = new Dictionary<string, IRuleConfiguration>(StringComparer.OrdinalIgnoreCase)
        {
            { "PS/AlignAssignmentStatement", DisabledEditorConfig<AlignAssignmentStatementEditorConfiguration>() },
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
            { "PS/PlaceCloseBrace", DisabledEditorConfig<PlaceCloseBraceEditorConfiguration>() },
            { "PS/PlaceOpenBrace", DisabledEditorConfig<PlaceOpenBraceEditorConfiguration>() },
            { "PS/PossibleIncorrectComparisonWithNull", null },
            { "PS/UseConsistentIndentation", DisabledEditorConfig<UseConsistentIndentationEditorConfiguration>() },
            { "PS/UseConsistentWhitespace", DisabledEditorConfig<UseConsistentWhitespaceEditorConfiguration>() },
            { "PS/UseCorrectCasing", DisabledEditorConfig<UseCorrectCasingEditorConfiguration>() },
            { "PS/UseDeclaredVarsMoreThanAssignments", null },
            { "PS/UseShouldProcessForStateChangingFunctions", null },
        };

        public static IRuleExecutorFactory RuleExecutorFactory { get; } = new ParallelLinqRuleExecutorFactory();

        public static RuleComponentProvider RuleComponentProvider => s_ruleComponentProviderLazy.Value;

        private static RuleComponentProvider BuildRuleComponentProvider()
        {
            return new RuleComponentProviderBuilder()
                .UseBuiltinDatabase()
                .Build();
        }
    }

}
