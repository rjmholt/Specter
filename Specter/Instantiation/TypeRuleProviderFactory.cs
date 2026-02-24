using Specter.Builder;
using Specter.Configuration;
using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Specter.Instantiation
{
    public class TypeRuleProviderFactoryBuilder
    {
        private readonly Dictionary<string, IRuleConfiguration?> _ruleConfigurationCollection;

        private readonly Dictionary<Type, RuleInfo?> _ruleTypes;

        public TypeRuleProviderFactoryBuilder(
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurationCollection)
        {
            _ruleTypes = new Dictionary<Type, RuleInfo?>();
            _ruleConfigurationCollection = ruleConfigurationCollection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public TypeRuleProviderFactoryBuilder AddRule<TRule>() where TRule : ScriptRule
        {
            _ruleTypes.Add(typeof(TRule), null);
            return this;
        }

        public TypeRuleProviderFactoryBuilder AddRule<TRule, TConfiguration>(TConfiguration configuration) where TRule : IConfigurableRule<TConfiguration> where TConfiguration : IRuleConfiguration
        {
            RuleInfo ruleInfo = RuleInfo.TryGetFromRuleType(typeof(TRule), out RuleInfo? ruleInfoOut)
                ? ruleInfoOut!
                : throw new ArgumentException($"Type '{typeof(TRule)}' is not a valid rule type");
            _ruleTypes.Add(typeof(TRule), ruleInfo);
            _ruleConfigurationCollection[ruleInfo.FullName] = configuration;
            return this;
        }

        public TypeRuleProviderFactory Build()
        {
            return new TypeRuleProviderFactory(_ruleConfigurationCollection, _ruleTypes);
        }
    }

    public class TypeRuleProviderFactory : IRuleProviderFactory
    {
        public static TypeRuleProviderFactory FromAssemblyFile(
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurationCollection,
            string assemblyPath)
        {
            return FromAssembly(ruleConfigurationCollection, Assembly.LoadFile(assemblyPath));
        }

        public static TypeRuleProviderFactory FromAssembly(
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurationCollection,
            Assembly ruleAssembly)
        {
            return new TypeRuleProviderFactory(ruleConfigurationCollection, ruleAssembly.GetExportedTypes().ToDictionary(t => t, _ => (RuleInfo?)null));
        }

        private readonly IReadOnlyDictionary<string, IRuleConfiguration?> _ruleConfigurationCollection;

        private readonly IReadOnlyDictionary<Type, RuleInfo?> _ruleTypes;

        public TypeRuleProviderFactory(
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfigurationCollection,
            IReadOnlyDictionary<Type, RuleInfo?> ruleTypes)
        {
            _ruleConfigurationCollection = ruleConfigurationCollection;
            _ruleTypes = ruleTypes;
        }

        public IRuleProvider CreateRuleProvider(RuleComponentProvider ruleComponentProvider)
        {
            return new TypeRuleProvider(GetRuleFactoriesFromTypes(ruleComponentProvider));
        }

        private IReadOnlyDictionary<RuleInfo, TypeRuleFactory<ScriptRule>> GetRuleFactoriesFromTypes(
            RuleComponentProvider ruleComponentProvider)
        {
            var ruleFactories = new Dictionary<RuleInfo, TypeRuleFactory<ScriptRule>>();

            foreach (KeyValuePair<Type, RuleInfo?> rule in _ruleTypes)
            {
                RuleInfo? ruleInfo = rule.Value;
                if (RuleGeneration.TryGetRuleFromType(
                    _ruleConfigurationCollection,
                    ruleComponentProvider,
                    rule.Key,
                    ref ruleInfo,
                    out TypeRuleFactory<ScriptRule>? factory)
                    && ruleInfo is not null
                    && factory is not null)
                {
                    ruleFactories[ruleInfo] = factory;
                }
            }

            return ruleFactories;
        }
    }

}
