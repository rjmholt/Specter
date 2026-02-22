using System;
using System.Collections.Generic;
using PSpecter.CommandDatabase;
using PSpecter.CommandDatabase.Sqlite;

namespace PSpecter.Builder
{
    public abstract class RuleComponentProvider
    {
        public bool TryGetComponentInstance<TComponent>(out TComponent? component)
        {
            if (!TryGetComponentInstance(typeof(TComponent), out object? componentObj))
            {
                component = default;
                return false;
            }

            component = (TComponent)componentObj!;
            return true;
        }

        public abstract bool TryGetComponentInstance(Type componentType, out object? component);
    }

    internal class SimpleRuleComponentProvider : RuleComponentProvider
    {
        private readonly IReadOnlyDictionary<Type, Func<object>> _componentRegistrations;

        private readonly IReadOnlyDictionary<Type, object> _singletonComponents;

        public SimpleRuleComponentProvider(
            IReadOnlyDictionary<Type, Func<object>> componentRegistrations,
            IReadOnlyDictionary<Type, object> singletonComponents)
        {
            _componentRegistrations = componentRegistrations;
            _singletonComponents = singletonComponents;
        }

        public override bool TryGetComponentInstance(Type componentType, out object? component)
        {
            if (_singletonComponents.TryGetValue(componentType, out component))
            {
                return true;
            }

            if (_componentRegistrations.TryGetValue(componentType, out Func<object>? componentFactory))
            {
                component = componentFactory();
                return true;
            }

            component = null;
            return false;
        }
    }

    public class RuleComponentProviderBuilder
    {
        private readonly Dictionary<Type, object> _singletonComponents;

        private readonly Dictionary<Type, Func<object>> _componentRegistrations;

        public RuleComponentProviderBuilder()
        {
            _singletonComponents = new Dictionary<Type, object>();
            _componentRegistrations = new Dictionary<Type, Func<object>>();
        }

        public RuleComponentProviderBuilder AddSingleton<T>() where T : new()
        {
            _singletonComponents[typeof(T)] = new T();
            return this;
        }

        public RuleComponentProviderBuilder AddSingleton<T>(T instance)
        {
            _singletonComponents[typeof(T)] = instance!;
            return this;
        }

        public RuleComponentProviderBuilder AddSingleton<TRegistered, TInstance>() where TInstance : TRegistered, new()
        {
            _singletonComponents[typeof(TRegistered)] = new TInstance();
            return this;
        }

        public RuleComponentProviderBuilder AddSingleton<TRegistered, TInstance>(TInstance instance)
        {
            if (instance is null) { throw new ArgumentNullException(nameof(instance)); }
            _singletonComponents[typeof(TRegistered)] = instance;
            return this;
        }

        public RuleComponentProviderBuilder AddSingleton(Type registeredType, object instance)
        {
            if (instance is null) { throw new ArgumentNullException(nameof(instance)); }
            if (!registeredType.IsAssignableFrom(instance.GetType()))
            {
                throw new ArgumentException($"Cannot register object '{instance}' of type '{instance.GetType()}' for type '{registeredType}'");
            }

            _singletonComponents[registeredType] = instance;
            return this;
        }

        public RuleComponentProviderBuilder AddScoped<T>() where T : new()
        {
            _componentRegistrations[typeof(T)] = () => new T();
            return this;
        }

        public RuleComponentProviderBuilder AddScoped<T>(Func<T> factory) where T : class
        {
            _componentRegistrations[typeof(T)] = factory;
            return this;
        }

        public RuleComponentProviderBuilder AddScoped<TRegistered, TInstance>() where TInstance : TRegistered, new()
        {
            _componentRegistrations[typeof(TRegistered)] = () => new TInstance();
            return this;
        }

        public RuleComponentProviderBuilder AddScoped<TRegistered, TInstance>(Func<TInstance> factory) where TInstance : class, TRegistered 
        {
            _componentRegistrations[typeof(TRegistered)] = factory;
            return this;
        }

        public RuleComponentProviderBuilder UseBuiltinDatabase()
            => AddSingleton<IPowerShellCommandDatabase>(BuiltinCommandDatabase.Instance);

        /// <summary>
        /// Use the shipped SQLite database at the default module location.
        /// </summary>
        public RuleComponentProviderBuilder UseSqliteDatabase()
            => AddSingleton<IPowerShellCommandDatabase>(
                BuiltinCommandDatabase.CreateWithDatabase(BuiltinCommandDatabase.FindDefaultDatabasePath() ?? throw new InvalidOperationException("Default database path could not be resolved.")));

        /// <summary>
        /// Use a SQLite database at the specified path.
        /// </summary>
        public RuleComponentProviderBuilder UseSqliteDatabase(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Database path must not be null or empty.", nameof(path));
            }

            return AddSingleton<IPowerShellCommandDatabase>(SqliteCommandDatabase.Open(path));
        }

        public RuleComponentProvider Build()
        {
            if (!_singletonComponents.ContainsKey(typeof(IPowerShellCommandDatabase))
                && !_componentRegistrations.ContainsKey(typeof(IPowerShellCommandDatabase)))
            {
                _singletonComponents[typeof(IPowerShellCommandDatabase)] = BuiltinCommandDatabase.Instance;
            }

            return new SimpleRuleComponentProvider(_componentRegistrations, _singletonComponents);
        }
    }
}
