using Microsoft.Data.Sqlite;

namespace PSpecter.CommandDatabase.Sqlite
{
    /// <summary>
    /// Defines the SQLite schema for the PSpecter command database.
    /// All table and column names are sourced from <see cref="Db"/> constants.
    /// </summary>
    internal static class CommandDatabaseSchema
    {
        public const int SchemaVersion = 2;

        public static void CreateTables(SqliteConnection connection)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = BuildCreateTablesSql();
            cmd.ExecuteNonQuery();
        }

        private static string BuildCreateTablesSql() =>
            "PRAGMA journal_mode = WAL;\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.SchemaVersionTable.Table} (" +
            $"  {Db.SchemaVersionTable.Version} INTEGER NOT NULL" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.Platform.Table} (" +
            $"  {Db.Platform.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.Platform.Edition} TEXT NOT NULL," +
            $"  {Db.Platform.Version} TEXT NOT NULL," +
            $"  {Db.Platform.OS} TEXT NOT NULL," +
            $"  UNIQUE({Db.Platform.Edition}, {Db.Platform.Version}, {Db.Platform.OS})" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.Module.Table} (" +
            $"  {Db.Module.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.Module.Name} TEXT NOT NULL," +
            $"  {Db.Module.Version} TEXT NOT NULL DEFAULT ''," +
            $"  UNIQUE({Db.Module.Name}, {Db.Module.Version})" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.Command.Table} (" +
            $"  {Db.Command.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.Command.ModuleId} INTEGER NOT NULL REFERENCES {Db.Module.Table}({Db.Module.Id})," +
            $"  {Db.Command.Name} TEXT NOT NULL," +
            $"  {Db.Command.CommandType} TEXT NOT NULL," +
            $"  {Db.Command.DefaultParameterSet} TEXT," +
            $"  UNIQUE({Db.Command.ModuleId}, {Db.Command.Name} COLLATE NOCASE)" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_Command_Name ON {Db.Command.Table}({Db.Command.Name} COLLATE NOCASE);\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.CommandPlatform.Table} (" +
            $"  {Db.CommandPlatform.CommandId} INTEGER NOT NULL REFERENCES {Db.Command.Table}({Db.Command.Id})," +
            $"  {Db.CommandPlatform.PlatformId} INTEGER NOT NULL REFERENCES {Db.Platform.Table}({Db.Platform.Id})," +
            $"  PRIMARY KEY ({Db.CommandPlatform.CommandId}, {Db.CommandPlatform.PlatformId})" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.Parameter.Table} (" +
            $"  {Db.Parameter.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.Parameter.CommandId} INTEGER NOT NULL REFERENCES {Db.Command.Table}({Db.Command.Id})," +
            $"  {Db.Parameter.Name} TEXT NOT NULL," +
            $"  {Db.Parameter.Type} TEXT," +
            $"  {Db.Parameter.IsDynamic} INTEGER NOT NULL DEFAULT 0," +
            $"  UNIQUE({Db.Parameter.CommandId}, {Db.Parameter.Name} COLLATE NOCASE)" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_Parameter_CommandId ON {Db.Parameter.Table}({Db.Parameter.CommandId});\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.ParameterPlatform.Table} (" +
            $"  {Db.ParameterPlatform.ParameterId} INTEGER NOT NULL REFERENCES {Db.Parameter.Table}({Db.Parameter.Id})," +
            $"  {Db.ParameterPlatform.PlatformId} INTEGER NOT NULL REFERENCES {Db.Platform.Table}({Db.Platform.Id})," +
            $"  PRIMARY KEY ({Db.ParameterPlatform.ParameterId}, {Db.ParameterPlatform.PlatformId})" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.ParameterSetMembership.Table} (" +
            $"  {Db.ParameterSetMembership.ParameterId} INTEGER NOT NULL REFERENCES {Db.Parameter.Table}({Db.Parameter.Id})," +
            $"  {Db.ParameterSetMembership.SetName} TEXT NOT NULL," +
            $"  {Db.ParameterSetMembership.Position} INTEGER," +
            $"  {Db.ParameterSetMembership.IsMandatory} INTEGER NOT NULL DEFAULT 0," +
            $"  {Db.ParameterSetMembership.ValueFromPipeline} INTEGER NOT NULL DEFAULT 0," +
            $"  {Db.ParameterSetMembership.ValueFromPipelineByPropertyName} INTEGER NOT NULL DEFAULT 0," +
            $"  PRIMARY KEY ({Db.ParameterSetMembership.ParameterId}, {Db.ParameterSetMembership.SetName})" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_PSM_ParameterId ON {Db.ParameterSetMembership.Table}({Db.ParameterSetMembership.ParameterId});\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.Alias.Table} (" +
            $"  {Db.Alias.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.Alias.Name} TEXT NOT NULL," +
            $"  {Db.Alias.CommandId} INTEGER NOT NULL REFERENCES {Db.Command.Table}({Db.Command.Id})," +
            $"  UNIQUE({Db.Alias.Name} COLLATE NOCASE, {Db.Alias.CommandId})" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_Alias_Name ON {Db.Alias.Table}({Db.Alias.Name} COLLATE NOCASE);\n" +
            $"CREATE INDEX IF NOT EXISTS IX_Alias_CommandId ON {Db.Alias.Table}({Db.Alias.CommandId});\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.AliasPlatform.Table} (" +
            $"  {Db.AliasPlatform.AliasId} INTEGER NOT NULL REFERENCES {Db.Alias.Table}({Db.Alias.Id})," +
            $"  {Db.AliasPlatform.PlatformId} INTEGER NOT NULL REFERENCES {Db.Platform.Table}({Db.Platform.Id})," +
            $"  PRIMARY KEY ({Db.AliasPlatform.AliasId}, {Db.AliasPlatform.PlatformId})" +
            $");\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.OutputType.Table} (" +
            $"  {Db.OutputType.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.OutputType.CommandId} INTEGER NOT NULL REFERENCES {Db.Command.Table}({Db.Command.Id})," +
            $"  {Db.OutputType.TypeName} TEXT NOT NULL," +
            $"  UNIQUE({Db.OutputType.CommandId}, {Db.OutputType.TypeName})" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_OutputType_CommandId ON {Db.OutputType.Table}({Db.OutputType.CommandId});\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.TypeAccelerator.Table} (" +
            $"  {Db.TypeAccelerator.Id} INTEGER PRIMARY KEY AUTOINCREMENT," +
            $"  {Db.TypeAccelerator.Name} TEXT NOT NULL," +
            $"  {Db.TypeAccelerator.FullName} TEXT NOT NULL" +
            $");\n" +
            $"CREATE INDEX IF NOT EXISTS IX_TypeAccelerator_Name ON {Db.TypeAccelerator.Table}({Db.TypeAccelerator.Name} COLLATE NOCASE);\n" +

            $"CREATE TABLE IF NOT EXISTS {Db.TypeAcceleratorPlatform.Table} (" +
            $"  {Db.TypeAcceleratorPlatform.TypeAcceleratorId} INTEGER NOT NULL REFERENCES {Db.TypeAccelerator.Table}({Db.TypeAccelerator.Id})," +
            $"  {Db.TypeAcceleratorPlatform.PlatformId} INTEGER NOT NULL REFERENCES {Db.Platform.Table}({Db.Platform.Id})," +
            $"  PRIMARY KEY ({Db.TypeAcceleratorPlatform.TypeAcceleratorId}, {Db.TypeAcceleratorPlatform.PlatformId})" +
            $");\n";
    }
}
