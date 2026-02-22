using System.Collections.Generic;

namespace PSpecter.CommandDatabase
{
    public sealed class CommandMetadata
    {
        private readonly List<string> _aliases;

        public CommandMetadata(
            string name,
            string commandType,
            string moduleName,
            string defaultParameterSet,
            IReadOnlyList<string> parameterSetNames,
            IReadOnlyList<string> aliases,
            IReadOnlyList<ParameterMetadata> parameters,
            IReadOnlyList<string> outputTypes)
        {
            Name = name;
            CommandType = commandType;
            ModuleName = moduleName;
            DefaultParameterSet = defaultParameterSet;
            ParameterSetNames = parameterSetNames ?? System.Array.Empty<string>();
            _aliases = aliases is not null ? new List<string>(aliases) : new List<string>();
            Parameters = parameters ?? System.Array.Empty<ParameterMetadata>();
            OutputTypes = outputTypes ?? System.Array.Empty<string>();
        }

        public string Name { get; }
        public string CommandType { get; }
        public string ModuleName { get; }
        public string DefaultParameterSet { get; }
        public IReadOnlyList<string> ParameterSetNames { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<ParameterMetadata> Parameters { get; }
        public IReadOnlyList<string> OutputTypes { get; }

        public void AddAlias(string alias) => _aliases.Add(alias);
    }

    public sealed class ParameterMetadata
    {
        public ParameterMetadata(
            string name,
            string type,
            bool isDynamic,
            IReadOnlyList<ParameterSetInfo> parameterSets)
        {
            Name = name;
            Type = type;
            IsDynamic = isDynamic;
            ParameterSets = parameterSets ?? System.Array.Empty<ParameterSetInfo>();
        }

        public string Name { get; }
        public string Type { get; }
        public bool IsDynamic { get; }
        public IReadOnlyList<ParameterSetInfo> ParameterSets { get; }
    }

    /// <summary>
    /// Per-parameter-set attributes. Position, Mandatory, and pipeline binding
    /// all vary depending on which parameter set the parameter belongs to.
    /// </summary>
    public sealed class ParameterSetInfo
    {
        public ParameterSetInfo(
            string setName,
            int? position,
            bool isMandatory,
            bool valueFromPipeline,
            bool valueFromPipelineByPropertyName)
        {
            SetName = setName;
            Position = position;
            IsMandatory = isMandatory;
            ValueFromPipeline = valueFromPipeline;
            ValueFromPipelineByPropertyName = valueFromPipelineByPropertyName;
        }

        public string SetName { get; }
        public int? Position { get; }
        public bool IsMandatory { get; }
        public bool ValueFromPipeline { get; }
        public bool ValueFromPipelineByPropertyName { get; }
    }
}
