using Specter.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Specter.Configuration.Json
{
    internal class JsonConfigurationConverter : JsonConverter<JsonScriptAnalyzerConfiguration>
    {
        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override JsonScriptAnalyzerConfiguration ReadJson(
            JsonReader reader,
            Type objectType,
            JsonScriptAnalyzerConfiguration? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            JObject configObject = JObject.Load(reader);

            var ruleConfigurationsObject = (JObject?)configObject[ConfigurationKeys.RuleConfigurations] ?? new JObject();
            var configDictionary = new Dictionary<string, JsonRuleConfiguration>(ruleConfigurationsObject.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, JToken?> configEntry in ruleConfigurationsObject)
            {
                if (configEntry.Value is not JObject ruleConfigObject)
                {
                    continue;
                }

                var commonConfiguration = ruleConfigObject[ConfigurationKeys.CommonConfiguration]?.ToObject<CommonConfiguration>() ?? CommonConfiguration.Default;

                configDictionary[configEntry.Key] = new JsonRuleConfiguration(commonConfiguration, ruleConfigObject);
            }

            return new JsonScriptAnalyzerConfiguration(
                configObject[ConfigurationKeys.BuiltinRulePreference]?.ToObject<BuiltinRulePreference>(),
                configObject[ConfigurationKeys.RuleExecutionMode]?.ToObject<RuleExecutionMode>(),
                configObject[ConfigurationKeys.RulePaths]?.ToObject<string[]>() ?? Polyfill.GetEmptyArray<string>(),
                configDictionary);
        }

        public override void WriteJson(JsonWriter writer, JsonScriptAnalyzerConfiguration? value, JsonSerializer serializer)
        {
            // Not needed - CanWrite is false
            throw new NotImplementedException();
        }
    }
}
