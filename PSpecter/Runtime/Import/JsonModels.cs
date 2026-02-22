using System.Collections.Generic;
using Newtonsoft.Json;

namespace PSpecter.Runtime.Import
{
    // ==========================================
    // Legacy settings format (Engine/Settings)
    // ==========================================

    internal sealed class LegacySettingsRoot
    {
        [JsonProperty("SchemaVersion")]
        public string SchemaVersion { get; set; }

        [JsonProperty("Modules")]
        public List<LegacyModule> Modules { get; set; }
    }

    internal sealed class LegacyModule
    {
        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Version")]
        public string Version { get; set; }

        [JsonProperty("ExportedCommands")]
        public List<LegacyCommand> ExportedCommands { get; set; }

        [JsonProperty("ExportedAliases")]
        public List<string> ExportedAliases { get; set; }
    }

    internal sealed class LegacyCommand
    {
        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("CommandType")]
        public string CommandType { get; set; }
    }

    // ==========================================
    // PSCompatibilityCollector profile format
    // ==========================================

    internal sealed class CompatProfileRoot
    {
        [JsonProperty("Platform")]
        public CompatPlatform Platform { get; set; }

        [JsonProperty("Runtime")]
        public CompatRuntime Runtime { get; set; }
    }

    internal sealed class CompatPlatform
    {
        [JsonProperty("PowerShell")]
        public CompatPowerShell PowerShell { get; set; }

        [JsonProperty("OperatingSystem")]
        public CompatOperatingSystem OperatingSystem { get; set; }
    }

    internal sealed class CompatPowerShell
    {
        [JsonProperty("Edition")]
        public string Edition { get; set; }

        /// <summary>
        /// Version may be either a plain string or a structured object with Major/Minor/Patch.
        /// We handle both cases in the importer using a custom converter.
        /// </summary>
        [JsonProperty("Version")]
        [JsonConverter(typeof(FlexibleVersionConverter))]
        public string Version { get; set; }
    }

    internal sealed class CompatOperatingSystem
    {
        [JsonProperty("Family")]
        public string Family { get; set; }
    }

    internal sealed class CompatRuntime
    {
        [JsonProperty("Modules")]
        public Dictionary<string, Dictionary<string, CompatModuleVersion>> Modules { get; set; }
    }

    internal sealed class CompatModuleVersion
    {
        [JsonProperty("Guid")]
        public string Guid { get; set; }

        [JsonProperty("Cmdlets")]
        public Dictionary<string, CompatCommandData> Cmdlets { get; set; }

        [JsonProperty("Functions")]
        public Dictionary<string, CompatCommandData> Functions { get; set; }

        [JsonProperty("Aliases")]
        public Dictionary<string, string> Aliases { get; set; }
    }

    internal sealed class CompatCommandData
    {
        [JsonProperty("OutputType")]
        public List<string> OutputType { get; set; }

        [JsonProperty("ParameterSets")]
        public List<string> ParameterSets { get; set; }

        [JsonProperty("DefaultParameterSet")]
        public string DefaultParameterSet { get; set; }

        [JsonProperty("Parameters")]
        public Dictionary<string, CompatParameterData> Parameters { get; set; }
    }

    internal sealed class CompatParameterData
    {
        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Dynamic")]
        public bool Dynamic { get; set; }

        [JsonProperty("ParameterSets")]
        public Dictionary<string, CompatParameterSetData> ParameterSets { get; set; }
    }

    internal sealed class CompatParameterSetData
    {
        [JsonProperty("Flags")]
        public List<string> Flags { get; set; }

        [JsonProperty("Position")]
        public int? Position { get; set; }
    }

    // ==========================================
    // Custom converter: handles version as string or {Major, Minor, Patch, ...}
    // ==========================================

    internal sealed class FlexibleVersionConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, System.Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = Newtonsoft.Json.Linq.JObject.Load(reader);
                int major = (int?)obj["Major"] ?? 0;
                int minor = (int?)obj["Minor"] ?? 0;
                int patch = (int?)obj["Patch"] ?? (int?)obj["Build"] ?? 0;
                var label = obj["Label"] ?? obj["PreReleaseLabel"];
                string ver = $"{major}.{minor}.{patch}";
                if (label is not null)
                {
                    string labelStr = label.ToString();
                    if (!string.IsNullOrWhiteSpace(labelStr))
                    {
                        ver += $"-{labelStr}";
                    }
                }
                return ver;
            }

            return reader.Value?.ToString() ?? "0.0.0";
        }

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
