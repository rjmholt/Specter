using System.Collections.Generic;
using Newtonsoft.Json;

namespace PSpecter.CommandDatabase.Import.CompatibilityProfile
{
    internal sealed class ProfileRoot
    {
        [JsonProperty("Platform")]
        public Platform Platform { get; set; }

        [JsonProperty("Runtime")]
        public Runtime Runtime { get; set; }
    }

    internal sealed class Platform
    {
        [JsonProperty("PowerShell")]
        public PowerShellInfo PowerShell { get; set; }

        [JsonProperty("OperatingSystem")]
        public OperatingSystemInfo OperatingSystem { get; set; }
    }

    internal sealed class PowerShellInfo
    {
        [JsonProperty("Edition")]
        public string Edition { get; set; }

        /// <summary>
        /// May be either a plain string or a structured object with Major/Minor/Patch.
        /// </summary>
        [JsonProperty("Version")]
        [JsonConverter(typeof(FlexibleVersionConverter))]
        public string Version { get; set; }
    }

    internal sealed class OperatingSystemInfo
    {
        [JsonProperty("Family")]
        public string Family { get; set; }
    }

    internal sealed class Runtime
    {
        [JsonProperty("Modules")]
        public Dictionary<string, Dictionary<string, ModuleVersion>> Modules { get; set; }
    }

    internal sealed class ModuleVersion
    {
        [JsonProperty("Guid")]
        public string Guid { get; set; }

        [JsonProperty("Cmdlets")]
        public Dictionary<string, CommandData> Cmdlets { get; set; }

        [JsonProperty("Functions")]
        public Dictionary<string, CommandData> Functions { get; set; }

        [JsonProperty("Aliases")]
        public Dictionary<string, string> Aliases { get; set; }
    }

    internal sealed class CommandData
    {
        [JsonProperty("OutputType")]
        public List<string> OutputType { get; set; }

        [JsonProperty("ParameterSets")]
        public List<string> ParameterSets { get; set; }

        [JsonProperty("DefaultParameterSet")]
        public string DefaultParameterSet { get; set; }

        [JsonProperty("Parameters")]
        public Dictionary<string, ParameterData> Parameters { get; set; }
    }

    internal sealed class ParameterData
    {
        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Dynamic")]
        public bool Dynamic { get; set; }

        [JsonProperty("ParameterSets")]
        public Dictionary<string, ParameterSetData> ParameterSets { get; set; }
    }

    internal sealed class ParameterSetData
    {
        [JsonProperty("Flags")]
        public List<string> Flags { get; set; }

        [JsonProperty("Position")]
        public int? Position { get; set; }
    }
}
