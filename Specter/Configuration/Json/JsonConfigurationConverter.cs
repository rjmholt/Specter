using Specter.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Specter.CommandDatabase;
using Specter.CommandDatabase.Import;
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

                var commonConfiguration = ruleConfigObject.ToObject<CommonConfiguration>() ?? CommonConfiguration.Default;

                configDictionary[configEntry.Key] = new JsonRuleConfiguration(commonConfiguration, ruleConfigObject);
            }

            return new JsonScriptAnalyzerConfiguration(
                configObject[ConfigurationKeys.BuiltinRulePreference]?.ToObject<BuiltinRulePreference>(),
                configObject[ConfigurationKeys.RuleExecutionMode]?.ToObject<RuleExecutionMode>(),
                configObject[ConfigurationKeys.RulePaths]?.ToObject<string[]>() ?? Polyfill.GetEmptyArray<string>(),
                ParseTargetPlatforms(configObject[ConfigurationKeys.TargetPlatforms]?.ToObject<string[]>()),
                configDictionary);
        }

        public override void WriteJson(JsonWriter writer, JsonScriptAnalyzerConfiguration? value, JsonSerializer serializer)
        {
            // Not needed - CanWrite is false
            throw new NotImplementedException();
        }

        private static IReadOnlyList<PlatformInfo>? ParseTargetPlatforms(string[]? profileNames)
        {
            if (profileNames is null || profileNames.Length == 0)
            {
                return null;
            }

            var targets = new List<PlatformInfo>(profileNames.Length);
            for (int i = 0; i < profileNames.Length; i++)
            {
                if (LegacySettingsImporter.TryParsePlatformFromFileName(profileNames[i], out PlatformInfo? platform)
                    && platform is not null)
                {
                    targets.Add(platform);
                }
            }

            return targets;
        }
    }
}
