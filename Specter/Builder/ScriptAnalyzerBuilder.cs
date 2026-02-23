using Specter.Builtin;
using Specter.Configuration;
using Specter.Execution;
using Specter.Instantiation;
using Specter.Logging;
using Specter.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Specter.Builder
{
    public class ScriptAnalyzerBuilder
    {
        private readonly List<IRuleProviderFactory> _ruleProviderFactories;

        private IRuleExecutorFactory? _ruleExecutorFactory;

        private RuleComponentProvider? _ruleComponentProvider;

        private IAnalysisLogger? _logger;

        private ExternalRulePolicy _externalRulePolicy = ExternalRulePolicy.Explicit;

        public ScriptAnalyzerBuilder()
        {
            _ruleProviderFactories = new List<IRuleProviderFactory>();
        }

        public ScriptAnalyzerBuilder WithLogger(IAnalysisLogger logger)
        {
            _logger = logger;
            return this;
        }

        public ScriptAnalyzerBuilder WithExternalRulePolicy(ExternalRulePolicy policy)
        {
            _externalRulePolicy = policy;
            return this;
        }

        public ScriptAnalyzerBuilder WithRuleExecutorFactory(IRuleExecutorFactory ruleExecutorFactory)
        {
            _ruleExecutorFactory = ruleExecutorFactory;
            return this;
        }

        public ScriptAnalyzerBuilder WithRuleComponentProvider(RuleComponentProvider ruleComponentProvider)
        {
            _ruleComponentProvider = ruleComponentProvider;
            return this;
        }

        public ScriptAnalyzerBuilder WithRuleComponentProvider(Action<RuleComponentProviderBuilder> configureComponentProviderBuilder)
        {
            var componentProviderBuilder = new RuleComponentProviderBuilder();
            configureComponentProviderBuilder(componentProviderBuilder);
            WithRuleComponentProvider(componentProviderBuilder.Build());
            return this;
        }

        public ScriptAnalyzerBuilder AddRuleProviderFactory(IRuleProviderFactory ruleProvider)
        {
            _ruleProviderFactories.Add(ruleProvider);
            return this;
        }

        public ScriptAnalyzerBuilder AddBuiltinRules()
            => AddBuiltinRules((IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration);

        public ScriptAnalyzerBuilder AddBuiltinRules(IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection)
        {
            _ruleProviderFactories.Add(
                new BuiltinRuleProviderFactory(ruleConfigurationCollection));
            return this;
        }

        public ScriptAnalyzerBuilder AddRules(Action<TypeRuleProviderFactoryBuilder> configureRuleProviderFactory)
            => AddRules((IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration, configureRuleProviderFactory);

        public ScriptAnalyzerBuilder AddRules(IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection, Action<TypeRuleProviderFactoryBuilder> configureRuleProviderFactory)
        {
            var ruleProviderFactoryBuilder = new TypeRuleProviderFactoryBuilder(ruleConfigurationCollection);
            configureRuleProviderFactory(ruleProviderFactoryBuilder);
            AddRuleProviderFactory(ruleProviderFactoryBuilder.Build());
            return this;
        }

        public ScriptAnalyzerBuilder AddRulesFromAssembly(Assembly ruleAssembly)
            => AddRulesFromAssembly((IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration, ruleAssembly);

        public ScriptAnalyzerBuilder AddRulesFromAssembly(IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection, Assembly ruleAssembly)
        {
            AddRuleProviderFactory(TypeRuleProviderFactory.FromAssembly(ruleConfigurationCollection, ruleAssembly));
            return this;
        }

        /// <summary>
        /// Load rules from an absolute path to a .dll, .psm1, .psd1, or directory.
        /// Applies path validation and ownership checks unless the policy is Unrestricted.
        /// Throws if the policy is Disabled.
        /// </summary>
        public ScriptAnalyzerBuilder AddRulesFromPath(string absolutePath)
            => AddRulesFromPath(absolutePath, (IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration);

        public ScriptAnalyzerBuilder AddRulesFromPath(
            string absolutePath,
            IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection)
        {
            EnsureExternalRulesAllowed();

            if (!Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException(
                    $"AddRulesFromPath requires an absolute path. Got: '{absolutePath}'",
                    nameof(absolutePath));
            }

            bool skipOwnershipCheck = _externalRulePolicy == ExternalRulePolicy.Unrestricted;

            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                absolutePath,
                settingsFileDirectory: null,
                ruleConfigurationCollection,
                skipOwnershipCheck,
                _logger);

            if (factory is not null)
            {
                _ruleProviderFactories.Add(factory);
            }

            return this;
        }

        /// <summary>
        /// Load rules from an absolute path to a directory, optionally recursing.
        /// </summary>
        public ScriptAnalyzerBuilder AddRulesFromPath(
            string absolutePath,
            bool recurse)
            => AddRulesFromPath(absolutePath, recurse, (IReadOnlyDictionary<string, IRuleConfiguration>)(object)Default.RuleConfiguration);

        public ScriptAnalyzerBuilder AddRulesFromPath(
            string absolutePath,
            bool recurse,
            IReadOnlyDictionary<string, IRuleConfiguration> ruleConfigurationCollection)
        {
            EnsureExternalRulesAllowed();

            if (!Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException(
                    $"AddRulesFromPath requires an absolute path. Got: '{absolutePath}'",
                    nameof(absolutePath));
            }

            bool skipOwnershipCheck = _externalRulePolicy == ExternalRulePolicy.Unrestricted;

            List<IRuleProviderFactory> factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                absolutePath,
                settingsFileDirectory: null,
                ruleConfigurationCollection,
                recurse,
                skipOwnershipCheck,
                _logger);

            _ruleProviderFactories.AddRange(factories);
            return this;
        }

        /// <summary>
        /// Load rules from a PowerShell module at an absolute path.
        /// </summary>
        public ScriptAnalyzerBuilder AddRulesFromModule(string absoluteModulePath)
        {
            EnsureExternalRulesAllowed();

            if (!Path.IsPathRooted(absoluteModulePath))
            {
                throw new ArgumentException(
                    $"AddRulesFromModule requires an absolute path. Got: '{absoluteModulePath}'",
                    nameof(absoluteModulePath));
            }

            _ruleProviderFactories.Add(new PSModuleRuleProviderFactory(absoluteModulePath, _logger));
            return this;
        }

        public ScriptAnalyzerBuilder AddBuiltinRules(Action<BuiltinRulesBuilder> configureBuiltinRules)
        {
            var builtinRulesBuilder = new BuiltinRulesBuilder();
            configureBuiltinRules(builtinRulesBuilder);
            _ruleProviderFactories.Add(builtinRulesBuilder.Build());
            return this;
        }

        public ScriptAnalyzer Build()
        {
            return ScriptAnalyzer.Create(_ruleComponentProvider!, _ruleExecutorFactory!, _ruleProviderFactories, _logger);
        }

        private void EnsureExternalRulesAllowed()
        {
            if (_externalRulePolicy == ExternalRulePolicy.Disabled)
            {
                throw new InvalidOperationException(
                    "External rule loading is disabled by the ExternalRules policy. " +
                    "Set ExternalRules to 'explicit' or 'unrestricted' to allow loading external rules.");
            }
        }
    }
}
