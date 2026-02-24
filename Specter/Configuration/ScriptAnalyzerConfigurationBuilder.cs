using Specter.Configuration.Json;
using Specter.Configuration.Psd;
using Specter.CommandDatabase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Specter.Configuration
{
    public class ScriptAnalyzerConfigurationBuilder
    {
        private readonly List<string> _rulePaths;

        private readonly Dictionary<string, IRuleConfiguration?> _ruleConfigurations;
        private readonly Dictionary<PlatformInfo, PlatformInfo> _targetPlatforms;

        private BuiltinRulePreference? _builtinRulePreference;

        private RuleExecutionMode? _executionMode;

        public ScriptAnalyzerConfigurationBuilder()
        {
            _rulePaths = new List<string>();
            _ruleConfigurations = new Dictionary<string, IRuleConfiguration?>();
            _targetPlatforms = new Dictionary<PlatformInfo, PlatformInfo>();
        }

        public ScriptAnalyzerConfigurationBuilder WithBuiltinRuleSet(BuiltinRulePreference builtinRulePreference)
        {
            _builtinRulePreference = builtinRulePreference;
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder WithRuleExecutionMode(RuleExecutionMode executionMode)
        {
            _executionMode = executionMode;
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddNonConfiguredRule(string ruleName)
        {
            _ruleConfigurations[ruleName] = CommonConfiguration.Default;
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddRuleConfiguration(string ruleName, IRuleConfiguration ruleConfiguration)
        {
            _ruleConfigurations[ruleName] = ruleConfiguration;
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddRuleConfigurations(IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurations)
        {
            foreach (KeyValuePair<string, IRuleConfiguration?> entry in ruleConfigurations)
            {
                _ruleConfigurations[entry.Key] = entry.Value;
            }
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddRulePath(string rulePath)
        {
            _rulePaths.Add(rulePath);
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddRulePaths(IEnumerable<string> rulePaths)
        {
            _rulePaths.AddRange(rulePaths);
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddTargetPlatforms(IEnumerable<PlatformInfo> targetPlatforms)
        {
            foreach (PlatformInfo platform in targetPlatforms)
            {
                _targetPlatforms[platform] = platform;
            }

            return this;
        }

        public ScriptAnalyzerConfigurationBuilder ExcludeRule(string rule)
        {
            _ruleConfigurations.Remove(rule);
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder ExcludeRules(IEnumerable<string> rules)
        {
            foreach (string rule in rules)
            {
                _ruleConfigurations.Remove(rule);
            }
            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddConfiguration(IScriptAnalyzerConfiguration configuration)
        {
            if (configuration.BuiltinRules != null)
            {
                WithBuiltinRuleSet(configuration.BuiltinRules.Value);
            }

            if (configuration.RuleExecution != null)
            {
                WithRuleExecutionMode(configuration.RuleExecution.Value);
            }

            AddRulePaths(configuration.RulePaths);
            if (configuration.TargetPlatforms is not null)
            {
                AddTargetPlatforms(configuration.TargetPlatforms);
            }
            AddRuleConfigurations(configuration.RuleConfiguration);

            return this;
        }

        public ScriptAnalyzerConfigurationBuilder AddConfiguration(Action<ScriptAnalyzerConfigurationBuilder> configureSubConfiguration)
        {
            var subConfiguration = new ScriptAnalyzerConfigurationBuilder();
            configureSubConfiguration(subConfiguration);
            return AddConfiguration(subConfiguration.Build());
        }

        public ScriptAnalyzerConfigurationBuilder AddConfigurationFile(string filePath)
        {
            if (string.Equals(Path.GetExtension(filePath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                AddConfiguration(JsonScriptAnalyzerConfiguration.FromFile(filePath));
            }
            else
            {
                AddConfiguration(PsdScriptAnalyzerConfiguration.FromFile(filePath));
            }

            return this;
        }

        public IScriptAnalyzerConfiguration Build()
        {
            var targetPlatforms = new List<PlatformInfo>(_targetPlatforms.Count);
            foreach (PlatformInfo platform in _targetPlatforms.Keys)
            {
                targetPlatforms.Add(platform);
            }

            return new MemoryScriptAnalyzerConfiguration(_builtinRulePreference, _executionMode, _rulePaths, targetPlatforms, _ruleConfigurations);
        }
    }

    internal class MemoryScriptAnalyzerConfiguration : IScriptAnalyzerConfiguration
    {
        public MemoryScriptAnalyzerConfiguration(
            BuiltinRulePreference? builtinRulePreference,
            RuleExecutionMode? ruleExecutionMode,
            IReadOnlyList<string> rulePaths,
            IReadOnlyList<PlatformInfo>? targetPlatforms,
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurations)
        {
            BuiltinRules = builtinRulePreference;
            RuleExecution = ruleExecutionMode;
            RulePaths = rulePaths;
            TargetPlatforms = targetPlatforms;
            RuleConfiguration = ruleConfigurations;
        }

        public IReadOnlyList<string> RulePaths { get; }

        public RuleExecutionMode? RuleExecution { get; }

        public IReadOnlyList<PlatformInfo>? TargetPlatforms { get; }

        public IReadOnlyDictionary<string, IRuleConfiguration?> RuleConfiguration { get; }

        public BuiltinRulePreference? BuiltinRules { get; }

        public ExternalRulePolicy ExternalRules => ExternalRulePolicy.Explicit;
    }
}
