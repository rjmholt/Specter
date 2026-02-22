using Microsoft.Data.Sqlite;

namespace PSpecter.Runtime
{
    /// <summary>
    /// Defines the SQLite schema for the PSpecter command database.
    /// All tables are platform-tagged so that a single database file can
    /// hold metadata for multiple PowerShell editions/versions/OSes.
    /// </summary>
    internal static class CommandDatabaseSchema
    {
        public const int SchemaVersion = 1;

        public static void CreateTables(SqliteConnection connection)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = CreateTablesSql;
            cmd.ExecuteNonQuery();
        }

        private const string CreateTablesSql = @"
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS SchemaVersion (
    Version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Platform (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Edition TEXT    NOT NULL,  -- 'Core' or 'Desktop'
    Version TEXT    NOT NULL,  -- e.g. '7.4.7'
    OS      TEXT    NOT NULL,  -- 'windows', 'macos', 'linux'
    UNIQUE(Edition, Version, OS)
);

CREATE TABLE IF NOT EXISTS Module (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT    NOT NULL,
    Version TEXT,
    UNIQUE(Name, Version)
);

CREATE TABLE IF NOT EXISTS Command (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    ModuleId            INTEGER REFERENCES Module(Id),
    Name                TEXT    NOT NULL,
    CommandType         TEXT    NOT NULL,  -- 'Cmdlet', 'Function', 'Alias', etc.
    DefaultParameterSet TEXT
);
CREATE INDEX IF NOT EXISTS IX_Command_Name ON Command(Name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS CommandPlatform (
    CommandId  INTEGER NOT NULL REFERENCES Command(Id),
    PlatformId INTEGER NOT NULL REFERENCES Platform(Id),
    PRIMARY KEY (CommandId, PlatformId)
);

CREATE TABLE IF NOT EXISTS Parameter (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    CommandId INTEGER NOT NULL REFERENCES Command(Id),
    Name      TEXT    NOT NULL,
    Type      TEXT,
    IsDynamic INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_Parameter_CommandId ON Parameter(CommandId);

CREATE TABLE IF NOT EXISTS ParameterPlatform (
    ParameterId INTEGER NOT NULL REFERENCES Parameter(Id),
    PlatformId  INTEGER NOT NULL REFERENCES Platform(Id),
    PRIMARY KEY (ParameterId, PlatformId)
);

CREATE TABLE IF NOT EXISTS ParameterSetMembership (
    ParameterId                    INTEGER NOT NULL REFERENCES Parameter(Id),
    SetName                        TEXT    NOT NULL,
    Position                       INTEGER,
    IsMandatory                    INTEGER NOT NULL DEFAULT 0,
    ValueFromPipeline              INTEGER NOT NULL DEFAULT 0,
    ValueFromPipelineByPropertyName INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (ParameterId, SetName)
);
CREATE INDEX IF NOT EXISTS IX_PSM_ParameterId ON ParameterSetMembership(ParameterId);

CREATE TABLE IF NOT EXISTS Alias (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT    NOT NULL,
    CommandId INTEGER NOT NULL REFERENCES Command(Id)
);
CREATE INDEX IF NOT EXISTS IX_Alias_Name ON Alias(Name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS IX_Alias_CommandId ON Alias(CommandId);

CREATE TABLE IF NOT EXISTS AliasPlatform (
    AliasId    INTEGER NOT NULL REFERENCES Alias(Id),
    PlatformId INTEGER NOT NULL REFERENCES Platform(Id),
    PRIMARY KEY (AliasId, PlatformId)
);

CREATE TABLE IF NOT EXISTS OutputType (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    CommandId INTEGER NOT NULL REFERENCES Command(Id),
    TypeName  TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_OutputType_CommandId ON OutputType(CommandId);

CREATE TABLE IF NOT EXISTS TypeAccelerator (
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    Name     TEXT NOT NULL,
    FullName TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_TypeAccelerator_Name ON TypeAccelerator(Name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS TypeAcceleratorPlatform (
    TypeAcceleratorId INTEGER NOT NULL REFERENCES TypeAccelerator(Id),
    PlatformId        INTEGER NOT NULL REFERENCES Platform(Id),
    PRIMARY KEY (TypeAcceleratorId, PlatformId)
);
";
    }
}
