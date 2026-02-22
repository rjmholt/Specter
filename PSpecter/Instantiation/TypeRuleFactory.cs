#nullable disable

using PSpecter.Builder;
using PSpecter.Configuration;
using PSpecter.Rules;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace PSpecter.Instantiation
{
    public abstract class TypeRuleFactory<TRule>
    {
        public TypeRuleFactory(RuleInfo ruleInfo, bool isEnabled)
        {
            RuleInfo = ruleInfo;
            IsEnabled = isEnabled;
        }

        public RuleInfo RuleInfo { get; }

        public bool IsEnabled { get; }

        public abstract TRule GetRuleInstance();

        public abstract void ReturnRuleInstance(TRule rule);
    }

    public class ConstructorInjectingRuleFactory<TRule> : TypeRuleFactory<TRule>
    {
        private readonly RuleComponentProvider _ruleComponentProvider;

        private readonly RuleInfo _ruleInfo;

        private readonly ConstructorInfo _ctorInfo;

        private readonly IRuleConfiguration _ruleConfiguration;

        private readonly Lazy<Func<TRule>> _factoryDelegateLazy;

        private int _callCount;

        public ConstructorInjectingRuleFactory(
            RuleComponentProvider ruleComponentProvider,
            RuleInfo ruleInfo,
            ConstructorInfo ctorInfo)
            : this(ruleComponentProvider, ruleInfo, ctorInfo, ruleConfiguration: null, isEnabled: true)
        {
        }

        public ConstructorInjectingRuleFactory(
            RuleComponentProvider ruleComponentProvider,
            RuleInfo ruleInfo,
            ConstructorInfo ctorInfo,
            IRuleConfiguration ruleConfiguration,
            bool isEnabled = true)
            : base(ruleInfo, isEnabled)
        {
            _ruleComponentProvider = ruleComponentProvider;
            _ruleInfo = ruleInfo;
            _ctorInfo = ctorInfo;
            _ruleConfiguration = ruleConfiguration;
            _factoryDelegateLazy = new Lazy<Func<TRule>>(CreateFactoryDelegate);
            _callCount = 0;
        }

        private Func<TRule> FactoryDelegate => _factoryDelegateLazy.Value;

        public override TRule GetRuleInstance() => InstantiateRuleInstance();

        public override void ReturnRuleInstance(TRule rule)
        {
            // Do nothing
        }

        protected TRule InstantiateRuleInstance()
        {
            // If the rule is being run repeatedly, optimise the constructor invocation
            return Interlocked.Increment(ref _callCount) > 4
                ? FactoryDelegate()
                : (TRule)_ctorInfo.Invoke(GetCtorArgs());
        }

        private object[] GetCtorArgs()
        {
            var ctorArgs = new List<object>();
            foreach (ParameterInfo ctorParameter in _ctorInfo.GetParameters())
            {
                if (ctorParameter.ParameterType == typeof(RuleInfo))
                {
                    ctorArgs.Add(_ruleInfo);
                    continue;
                }

                if (_ruleConfiguration != null
                    && ctorParameter.ParameterType == _ruleConfiguration.GetType())
                {
                    ctorArgs.Add(_ruleConfiguration);
                    continue;
                }

                if (typeof(IRuleConfiguration).IsAssignableFrom(ctorParameter.ParameterType)
                    && _ruleConfiguration is null)
                {
                    ctorArgs.Add(Activator.CreateInstance(ctorParameter.ParameterType));
                    continue;
                }

                if (_ruleComponentProvider.TryGetComponentInstance(ctorParameter.ParameterType, out object ctorArg))
                {
                    ctorArgs.Add(ctorArg);
                    continue;
                }

                throw new ArgumentException($"Rule constructor requires unknown argument: '{ctorParameter.Name}' of type '{ctorParameter.ParameterType.FullName}'");
            }

            return ctorArgs.ToArray();
        }

        private Func<TRule> CreateFactoryDelegate()
        {
            MethodInfo getCtorArgsMethod = typeof(ConstructorInjectingRuleFactory<>).GetMethod(
                nameof(GetCtorArgs),
                BindingFlags.NonPublic | BindingFlags.Instance);

            MethodCallExpression getArgsCall = Expression.Call(getCtorArgsMethod);

            return Expression.Lambda<Func<TRule>>(
                Expression.New(
                    _ctorInfo,
                    getArgsCall)).Compile();
        }
    }

    public class ConstructorInjectingDisposableRuleFactory<TRule> : ConstructorInjectingRuleFactory<TRule>
    {
        public ConstructorInjectingDisposableRuleFactory(
            RuleComponentProvider ruleComponentProvider,
            RuleInfo ruleInfo,
            ConstructorInfo ctorInfo,
            IRuleConfiguration ruleConfiguration,
            bool isEnabled = true)
            : base(ruleComponentProvider, ruleInfo, ctorInfo, ruleConfiguration, isEnabled)
        {
        }

        public override void ReturnRuleInstance(TRule rule)
        {
            ((IDisposable)rule).Dispose();
        }
    }

    public class ConstructorInjectionIdempotentRuleFactory<TRule> : ConstructorInjectingRuleFactory<TRule>
    {
        private readonly TRule _instance;

        public ConstructorInjectionIdempotentRuleFactory(
            RuleComponentProvider ruleComponentProvider,
            RuleInfo ruleInfo,
            ConstructorInfo ctorInfo,
            IRuleConfiguration ruleConfiguration,
            bool isEnabled = true)
            : base(ruleComponentProvider, ruleInfo, ctorInfo, ruleConfiguration, isEnabled)
        {
            _instance = isEnabled ? InstantiateRuleInstance() : default;
        }

        public override TRule GetRuleInstance()
        {
            return _instance;
        }
    }

    public class ConstructorInjectingResettableRulePoolingFactory<TRule> : ConstructorInjectingRuleFactory<TRule>
    {
        private readonly ResettablePool _pool;

        public ConstructorInjectingResettableRulePoolingFactory(
            RuleComponentProvider ruleComponentProvider,
            RuleInfo ruleInfo,
            ConstructorInfo ctorInfo,
            IRuleConfiguration ruleConfiguration,
            bool isEnabled = true)
            : base(ruleComponentProvider, ruleInfo, ctorInfo, ruleConfiguration, isEnabled)
        {
            _pool = isEnabled ? new ResettablePool(() => (IResettable)InstantiateRuleInstance()) : null;
        }

        public override TRule GetRuleInstance()
        {
            return (TRule)_pool.Take();
        }

        public override void ReturnRuleInstance(TRule rule)
        {
            _pool.Release((IResettable)rule);
        }
    }

    internal class ResettablePool
    {
        private readonly Func<IResettable> _factory;

        private readonly ConcurrentBag<IResettable> _instances;

        public ResettablePool(Func<IResettable> factory)
        {
            _factory = factory;
            _instances = new ConcurrentBag<IResettable>();
        }

        public IResettable Take()
        {
            if (_instances.TryTake(out IResettable instance))
            {
                return instance;
            }

            return _factory();
        }

        public void Release(IResettable instance)
        {
            instance.Reset();
            _instances.Add(instance);
        }
    }
}
