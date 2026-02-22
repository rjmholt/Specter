#nullable disable

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PSpecter.CommandDatabase.Import.CompatibilityProfile
{
    /// <summary>
    /// Handles PowerShell version fields that may be either a plain string
    /// or a structured object with Major/Minor/Patch/Label properties.
    /// </summary>
    internal sealed class FlexibleVersionConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
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
