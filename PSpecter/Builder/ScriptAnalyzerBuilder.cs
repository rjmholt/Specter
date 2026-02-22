using PSpecter.Builtin;
using PSpecter.Configuration;
using PSpecter.Execution;
using PSpecter.Instantiation;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PSpecter.Builder
{
    public class ScriptAnalyzerBuilder
    {
        private readonly List<IRuleProviderFactory> _ruleProviderFactories;

        private IRuleExecutorFactory? _ruleExecutorFactory;

        private RuleComponentProvider? _ruleComponentProvider;

        public ScriptAnalyzerBuilder()
        {
            _ruleProviderFactories = new List<IRuleProviderFactory>();
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

        public ScriptAnalyzerBuilder AddBuiltinRules(Action<BuiltinRulesBuilder> configureBuiltinRules)
        {
            var builtinRulesBuilder = new BuiltinRulesBuilder();
            configureBuiltinRules(builtinRulesBuilder);
            _ruleProviderFactories.Add(builtinRulesBuilder.Build());
            return this;
        }

        public ScriptAnalyzer Build()
        {
            return ScriptAnalyzer.Create(_ruleComponentProvider!, _ruleExecutorFactory!, _ruleProviderFactories);
        }
    }
}
