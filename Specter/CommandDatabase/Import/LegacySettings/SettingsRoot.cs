using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Specter.CommandDatabase.Import.LegacySettings
{
    internal class SettingsRoot
    {
        [JsonProperty("Modules")]
        public List<Module>? Modules { get; set; }
    }

    internal class Module
    {
        [JsonProperty("Name")]
        public string? Name { get; set; }

        [JsonProperty("Version")]
        public string? Version { get; set; }

        [JsonProperty("ExportedCommands")]
        [JsonConverter(typeof(SingleOrArrayConverter<Command>))]
        public List<Command>? ExportedCommands { get; set; }

        [JsonProperty("ExportedAliases")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? ExportedAliases { get; set; }
    }

    internal class Command
    {
        [JsonProperty("Name")]
        public string? Name { get; set; }

        [JsonProperty("CommandType")]
        public string? CommandType { get; set; }
    }

    /// <summary>
    /// Handles JSON values that can be either a single item or an array.
    /// </summary>
    internal class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T>? ReadJson(JsonReader reader, Type objectType, List<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>(serializer);
            }

            var item = token.ToObject<T>(serializer);
            return item is null ? new List<T>() : new List<T> { item };
        }

        public override void WriteJson(JsonWriter writer, List<T>? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
