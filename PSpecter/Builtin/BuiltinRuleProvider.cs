#nullable disable

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
            typeof(AvoidAssignmentToAutomaticVariable),
            typeof(AvoidDefaultTrueValueSwitchParameter),
            typeof(AvoidDefaultValueForMandatoryParameter),
            typeof(AvoidEmptyCatchBlock),
            typeof(AvoidExclaimOperator),
            typeof(AvoidGlobalAliases),
            typeof(AvoidGlobalFunctions),
            typeof(AvoidInvokingEmptyMembers),
            typeof(AvoidLongLines),
            typeof(AvoidReservedCharInCmdlet),
            typeof(AvoidReservedParams),
            typeof(AvoidUsingAllowUnencryptedAuthentication),
            typeof(AvoidSemicolonsAsLineTerminators),
            typeof(AvoidUsingBrokenHashAlgorithms),
            typeof(AvoidUsingComputerNameHardcoded),
            typeof(AvoidUsingDoubleQuotesForConstantString),
            typeof(AvoidUsingPlainTextForPassword),
            typeof(AvoidUserNameAndPasswordParams),
            typeof(AvoidGlobalVars),
            typeof(AvoidMultipleTypeAttributes),
            typeof(AvoidShouldContinueWithoutForce),
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
            typeof(PossibleIncorrectUsageOfAssignmentOperator),
            typeof(PossibleIncorrectUsageOfRedirectionOperator),
            typeof(UseLiteralInitializerForHashtable),
            typeof(UseConsistentIndentation),
            typeof(UseConsistentWhitespace),
            typeof(UseCorrectCasing),
            typeof(UseDeclaredVarsMoreThanAssignments),
            typeof(UsePSCredentialType),
            typeof(UseProcessBlockForPipelineCommand),
            typeof(UseApprovedVerbs),
            typeof(UseShouldProcessForStateChangingFunctions),
            typeof(AvoidOverwritingBuiltInCmdlets),
            typeof(UseBOMForUnicodeEncodedFile),
            typeof(UseUTF8EncodingForHelpFile),
            typeof(UseUsingScopeModifierInNewRunspaces),
            typeof(ReviewUnusedParameter),
            typeof(UseSingularNouns),
            typeof(UseSupportsShouldProcess),
            typeof(AvoidUsingDeprecatedManifestFields),
            typeof(UseCompatibleSyntax),
            typeof(UseCompatibleCmdlets),
            typeof(UseToExportFieldsInManifest),
            typeof(MissingModuleManifestField),
            typeof(ProvideCommentHelp),
            typeof(UseShouldProcessCorrectly),
            typeof(UseOutputTypeCorrectly),
            typeof(UseCmdletCorrectly),
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
            { "PS/AvoidExclaimOperator", new AvoidExclaimOperatorConfiguration { Common = new CommonConfiguration(enabled: false) } },
            { "PS/AvoidGlobalAliases", null },
            { "PS/AvoidGlobalFunctions", null },
            { "PS/AvoidMultipleTypeAttributes", null },
            { "PS/AvoidNullOrEmptyHelpMessageAttribute", null },
            { "PS/AvoidTrailingWhitespace", null },
            { "PS/AvoidUsingCmdletAliases", null },
            { "PS/AvoidUsingConvertToSecureStringWithPlainText", null },
            { "PS/AvoidUsingEmptyCatchBlock", null },
            { "PS/AvoidUsingInvokeExpression", null },
            { "PS/AvoidUsingAllowUnencryptedAuthentication", null },
            { "PS/AvoidUsingWriteHost", null },
            { "PS/AvoidAssignmentToAutomaticVariable", null },
            { "PS/AvoidInvokingEmptyMembers", null },
            { "PS/AvoidLongLines", new AvoidLongLinesConfiguration { Common = new CommonConfiguration(enabled: false) } },
            { "PS/AvoidSemicolonsAsLineTerminators", new CommonConfiguration(enabled: false) },
            { "PS/AvoidUsingBrokenHashAlgorithms", null },
            { "PS/AvoidUsingDoubleQuotesForConstantString", new CommonConfiguration(enabled: false) },
            { "PS/AvoidUsingComputerNameHardcoded", null },
            { "PS/AvoidUsingPlainTextForPassword", null },
            { "PS/AvoidUsingUsernameAndPasswordParams", null },
            { "PS/AvoidGlobalVars", null },
            { "PS/AvoidShouldContinueWithoutForce", null },
            { "PS/AvoidUsingPositionalParameters", new AvoidPositionalParametersConfiguration() },
            { "PS/AvoidUsingWMICmdlet", null },
            { "PS/PossibleIncorrectUsageOfAssignmentOperator", null },
            { "PS/PossibleIncorrectUsageOfRedirectionOperator", null },
            { "PS/UseLiteralInitializerForHashtable", null },
            { "PS/UsePSCredentialType", null },
            { "PS/MisleadingBacktick", null },
            { "PS/PlaceCloseBrace", DisabledEditorConfig<PlaceCloseBraceEditorConfiguration>() },
            { "PS/PlaceOpenBrace", DisabledEditorConfig<PlaceOpenBraceEditorConfiguration>() },
            { "PS/PossibleIncorrectComparisonWithNull", null },
            { "PS/ReservedCmdletChar", null },
            { "PS/ReservedParams", null },
            { "PS/UseConsistentIndentation", DisabledEditorConfig<UseConsistentIndentationEditorConfiguration>() },
            { "PS/UseConsistentWhitespace", DisabledEditorConfig<UseConsistentWhitespaceEditorConfiguration>() },
            { "PS/UseCorrectCasing", DisabledEditorConfig<UseCorrectCasingEditorConfiguration>() },
            { "PS/UseApprovedVerbs", null },
            { "PS/UseDeclaredVarsMoreThanAssignments", null },
            { "PS/UseProcessBlockForPipelineCommand", null },
            { "PS/UseShouldProcessForStateChangingFunctions", null },
            { "PS/AvoidOverwritingBuiltInCmdlets", new AvoidOverwritingBuiltInCmdletsConfiguration() },
            { "PS/UseBOMForUnicodeEncodedFile", null },
            { "PS/UseUTF8EncodingForHelpFile", null },
            { "PS/UseUsingScopeModifierInNewRunspaces", null },
            { "PS/ReviewUnusedParameter", new ReviewUnusedParameterConfiguration() },
            { "PS/UseSingularNouns", new UseSingularNounsConfiguration() },
            { "PS/UseSupportsShouldProcess", null },
            { "PS/AvoidUsingDeprecatedManifestFields", null },
            { "PS/UseCompatibleSyntax", new UseCompatibleSyntaxConfiguration() },
            { "PS/UseCompatibleCmdlets", new UseCompatibleCmdletsConfiguration() },
            { "PS/UseToExportFieldsInManifest", null },
            { "PS/MissingModuleManifestField", null },
            { "PS/ProvideCommentHelp", new ProvideCommentHelpConfiguration() },
            { "PS/ShouldProcess", null },
            { "PS/UseOutputTypeCorrectly", null },
            { "PS/UseCmdletCorrectly", null },
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
