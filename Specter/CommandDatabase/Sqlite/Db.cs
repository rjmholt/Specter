namespace Specter.CommandDatabase.Sqlite
{
    /// <summary>
    /// Typed constants for every table and column in the command database schema.
    /// All SQL in the writer and reader references these constants so that renaming
    /// a column produces a compile error rather than a silent runtime mismatch.
    /// </summary>
    internal static class Db
    {
        internal static class SchemaVersionTable
        {
            internal const string Table = "SchemaVersion";
            internal const string Version = "Version";
        }

        internal static class Platform
        {
            internal const string Table = "Platform";
            internal const string Id = "Id";
            internal const string Edition = "Edition";
            internal const string PsVersionMajor = "PsVersionMajor";
            internal const string PsVersionMinor = "PsVersionMinor";
            internal const string PsVersionBuild = "PsVersionBuild";
            internal const string PsVersionRevision = "PsVersionRevision";
            internal const string OsFamily = "OsFamily";
            internal const string OsVersion = "OsVersion";
            internal const string OsSkuId = "OsSkuId";
            internal const string Architecture = "Architecture";
            internal const string Environment = "Environment";
        }

        internal static class Module
        {
            internal const string Table = "Module";
            internal const string Id = "Id";
            internal const string Name = "Name";
            internal const string Version = "Version";
        }

        internal static class Command
        {
            internal const string Table = "Command";
            internal const string Id = "Id";
            internal const string ModuleId = "ModuleId";
            internal const string Name = "Name";
            internal const string CommandType = "CommandType";
            internal const string DefaultParameterSet = "DefaultParameterSet";
        }

        internal static class CommandPlatform
        {
            internal const string Table = "CommandPlatform";
            internal const string CommandId = "CommandId";
            internal const string PlatformId = "PlatformId";
        }

        internal static class Parameter
        {
            internal const string Table = "Parameter";
            internal const string Id = "Id";
            internal const string CommandId = "CommandId";
            internal const string Name = "Name";
            internal const string Type = "Type";
            internal const string IsDynamic = "IsDynamic";
        }

        internal static class ParameterPlatform
        {
            internal const string Table = "ParameterPlatform";
            internal const string ParameterId = "ParameterId";
            internal const string PlatformId = "PlatformId";
        }

        internal static class ParameterSetMembership
        {
            internal const string Table = "ParameterSetMembership";
            internal const string ParameterId = "ParameterId";
            internal const string SetName = "SetName";
            internal const string Position = "Position";
            internal const string IsMandatory = "IsMandatory";
            internal const string ValueFromPipeline = "ValueFromPipeline";
            internal const string ValueFromPipelineByPropertyName = "ValueFromPipelineByPropertyName";
        }

        internal static class Alias
        {
            internal const string Table = "Alias";
            internal const string Id = "Id";
            internal const string Name = "Name";
            internal const string CommandId = "CommandId";
        }

        internal static class AliasPlatform
        {
            internal const string Table = "AliasPlatform";
            internal const string AliasId = "AliasId";
            internal const string PlatformId = "PlatformId";
        }

        internal static class OutputType
        {
            internal const string Table = "OutputType";
            internal const string Id = "Id";
            internal const string CommandId = "CommandId";
            internal const string TypeName = "TypeName";
        }

        internal static class TypeAccelerator
        {
            internal const string Table = "TypeAccelerator";
            internal const string Id = "Id";
            internal const string Name = "Name";
            internal const string FullName = "FullName";
        }

        internal static class TypeAcceleratorPlatform
        {
            internal const string Table = "TypeAcceleratorPlatform";
            internal const string TypeAcceleratorId = "TypeAcceleratorId";
            internal const string PlatformId = "PlatformId";
        }

        internal static class ProfileName
        {
            internal const string Table = "ProfileName";
            internal const string Id = "Id";
            internal const string Name = "Name";
            internal const string PlatformId = "PlatformId";
        }
    }
}
