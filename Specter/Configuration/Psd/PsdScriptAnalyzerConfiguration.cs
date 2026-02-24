using Specter.Tools;
using Specter.CommandDatabase;
using Specter.CommandDatabase.Import;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Specter.Configuration.Psd
{
    public class PsdScriptAnalyzerConfiguration : IScriptAnalyzerConfiguration
    {
        public static PsdScriptAnalyzerConfiguration FromFile(string filePath)
        {
            return FromAst(PowerShellParsing.ParseHashtableFromFile(filePath));
        }

        public static PsdScriptAnalyzerConfiguration FromString(string hashtableString)
        {
            return FromAst(PowerShellParsing.ParseHashtableFromInput(hashtableString));
        }

        public static PsdScriptAnalyzerConfiguration FromAst(HashtableAst ast)
        {
            var psdConverter = new PsdTypedObjectConverter();

            var configuration = psdConverter.Convert<IReadOnlyDictionary<string, ExpressionAst>>(ast);
            var builtinRulePreference = psdConverter.Convert<BuiltinRulePreference?>(configuration[ConfigurationKeys.BuiltinRulePreference]);
            var ruleExecutionMode = psdConverter.Convert<RuleExecutionMode?>(configuration[ConfigurationKeys.RuleExecutionMode]);
            var rulePaths = psdConverter.Convert<IReadOnlyList<string>>(configuration[ConfigurationKeys.RulePaths]);
            IReadOnlyList<PlatformInfo>? targetPlatforms = null;
            if (configuration.TryGetValue(ConfigurationKeys.TargetPlatforms, out ExpressionAst? targetPlatformsAst)
                && targetPlatformsAst is not null)
            {
                IReadOnlyList<string> platformNames = psdConverter.Convert<IReadOnlyList<string>>(targetPlatformsAst);
                targetPlatforms = ParseTargetPlatforms(platformNames);
            }
            var ruleConfigurations = psdConverter.Convert<IReadOnlyDictionary<string, HashtableAst>>(configuration[ConfigurationKeys.RuleConfigurations]);

            return new PsdScriptAnalyzerConfiguration(psdConverter, builtinRulePreference, ruleExecutionMode, rulePaths, targetPlatforms, ruleConfigurations);
        }

        private readonly IReadOnlyDictionary<string, HashtableAst> _ruleConfigurations;

        private readonly ConcurrentDictionary<string, IRuleConfiguration> _ruleConfigurationCache;

        private readonly PsdTypedObjectConverter _psdConverter;

        public PsdScriptAnalyzerConfiguration(
            PsdTypedObjectConverter psdConverter,
            BuiltinRulePreference? builtinRulePreference,
            RuleExecutionMode? ruleExecutionMode,
            IReadOnlyList<string> rulePaths,
            IReadOnlyList<PlatformInfo>? targetPlatforms,
            IReadOnlyDictionary<string, HashtableAst> ruleConfigurations)
        {
            _ruleConfigurations = ruleConfigurations;
            _ruleConfigurationCache = new ConcurrentDictionary<string, IRuleConfiguration>();
            _psdConverter = psdConverter;
            BuiltinRules = builtinRulePreference;
            RuleExecution = ruleExecutionMode;
            RulePaths = rulePaths;
            TargetPlatforms = targetPlatforms;
            var ruleConfigDict = new Dictionary<string, IRuleConfiguration?>(ruleConfigurations.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ruleConfigurations)
            {
                CommonConfiguration common;
                try
                {
                    common = psdConverter.Convert<CommonConfiguration>(kvp.Value);
                }
                catch
                {
                    common = CommonConfiguration.Default;
                }

                ruleConfigDict[kvp.Key] = new PsdRuleConfiguration(psdConverter, common, kvp.Value);
            }
            RuleConfiguration = ruleConfigDict;
        }

        public BuiltinRulePreference? BuiltinRules { get; }

        public RuleExecutionMode? RuleExecution { get; }

        public IReadOnlyList<string> RulePaths { get; }

        public IReadOnlyList<PlatformInfo>? TargetPlatforms { get; }

        public IReadOnlyDictionary<string, IRuleConfiguration?> RuleConfiguration { get; }

        public ExternalRulePolicy ExternalRules => ExternalRulePolicy.Explicit;

        private static IReadOnlyList<PlatformInfo>? ParseTargetPlatforms(IReadOnlyList<string>? profileNames)
        {
            if (profileNames is null || profileNames.Count == 0)
            {
                return null;
            }

            var platforms = new List<PlatformInfo>(profileNames.Count);
            for (int i = 0; i < profileNames.Count; i++)
            {
                if (LegacySettingsImporter.TryParsePlatformFromFileName(profileNames[i], out PlatformInfo? platform)
                    && platform is not null)
                {
                    platforms.Add(platform);
                }
            }

            return platforms;
        }
    }

    public class PsdRuleConfiguration : LazyConvertedRuleConfiguration<HashtableAst>
    {
        private readonly PsdTypedObjectConverter _psdConverter;

        public PsdRuleConfiguration(
            PsdTypedObjectConverter psdConverter,
            CommonConfiguration common,
            HashtableAst configurationHashtableAst)
            : base(common, configurationHashtableAst)
        {
            _psdConverter = psdConverter;
        }

        public override bool TryConvertObject(Type type, HashtableAst configuration, out IRuleConfiguration? result)
        {
            try
            {
                result = (IRuleConfiguration?)_psdConverter.Convert(type, configuration);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }
}
