using Specter.Configuration;
using Specter.Execution;
using Specter.Instantiation;
using Specter.Security;
using System;
using System.Collections.Generic;
using System.IO;

namespace Specter.Builder
{
    public static class ConfiguredBuilding
    {
        public static ScriptAnalyzer CreateScriptAnalyzer(this IScriptAnalyzerConfiguration configuration)
            => CreateScriptAnalyzer(configuration, settingsFileDirectory: null);

        public static ScriptAnalyzer CreateScriptAnalyzer(
            this IScriptAnalyzerConfiguration configuration,
            string? settingsFileDirectory)
        {
            var analyzerBuilder = new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build());

            switch (configuration.BuiltinRules ?? BuiltinRulePreference.Default)
            {
                case BuiltinRulePreference.Aggressive:
                case BuiltinRulePreference.Default:
                    analyzerBuilder.AddBuiltinRules();
                    break;
            }

            switch (configuration.RuleExecution ?? RuleExecutionMode.Default)
            {
                case RuleExecutionMode.Default:
                case RuleExecutionMode.Parallel:
                    analyzerBuilder.WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory());
                    break;

                case RuleExecutionMode.Sequential:
                    analyzerBuilder.WithRuleExecutorFactory(new SequentialRuleExecutorFactory());
                    break;
            }

            ExternalRulePolicy policy = configuration.ExternalRules;
            analyzerBuilder.WithExternalRulePolicy(policy);

            if (policy != ExternalRulePolicy.Disabled && configuration.RulePaths is not null)
            {
                bool skipOwnershipCheck = policy == ExternalRulePolicy.Unrestricted;
                IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfiguration = configuration.RuleConfiguration;

                foreach (string rulePath in configuration.RulePaths)
                {
                    IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                        rulePath,
                        settingsFileDirectory,
                        ruleConfiguration,
                        skipOwnershipCheck,
                        logger: null);

                    if (factory is not null)
                    {
                        analyzerBuilder.AddRuleProviderFactory(factory);
                    }
                }
            }

            return analyzerBuilder.Build();
        }
    }
}
