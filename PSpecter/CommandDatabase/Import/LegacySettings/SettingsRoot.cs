using System.Collections.Generic;
using Newtonsoft.Json;

namespace PSpecter.CommandDatabase.Import.LegacySettings
{
    internal class SettingsRoot
    {
        [JsonProperty("Modules")]
        public List<Module> Modules { get; set; }
    }

    internal class Module
    {
        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Version")]
        public string Version { get; set; }

        [JsonProperty("ExportedCommands")]
        public List<Command> ExportedCommands { get; set; }

        [JsonProperty("ExportedAliases")]
        public List<string> ExportedAliases { get; set; }
    }

    internal class Command
    {
        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("CommandType")]
        public string CommandType { get; set; }
    }
}
