using PSpecter.Builtin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSpecter.Configuration
{
    public class CommonConfiguration : IRuleConfiguration
    {
        public static CommonConfiguration Default = new CommonConfiguration(enabled: true);

        [JsonConstructor]
        public CommonConfiguration(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; set; } = true;

        public CommonConfiguration Common => this;
    }

    public interface IRuleConfiguration
    {
        CommonConfiguration Common { get; }
    }

    public abstract class LazyConvertedRuleConfiguration<TConfiguration> : IRuleConfiguration
    {
        private readonly TConfiguration _configurationObject;

        private IRuleConfiguration _convertedObject;

        private Type _convertedObjectType;

        protected LazyConvertedRuleConfiguration(
            CommonConfiguration commonConfiguration,
            TConfiguration configurationObject)
        {
            _configurationObject = configurationObject;
            Common = commonConfiguration;
        }

        public CommonConfiguration Common { get; }

        public abstract bool TryConvertObject(Type type, TConfiguration configuration, out IRuleConfiguration result);

        public IRuleConfiguration AsTypedConfiguration(Type configurationType)
        {
            if (_convertedObject != null
                && configurationType.IsAssignableFrom(_convertedObjectType))
            {
                return _convertedObject;
            }

            if (TryConvertObject(configurationType, _configurationObject, out _convertedObject))
            {
                _convertedObjectType = configurationType;
                return _convertedObject;
            }

            return null;
        }
    }
}
