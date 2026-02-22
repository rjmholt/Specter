using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PSpecter.Runtime
{
    /// <summary>
    /// Bulk-inserts command metadata into the PSpecter SQLite database.
    /// Designed for use during database creation and update operations.
    /// </summary>
    public sealed class CommandDatabaseWriter : IDisposable
    {
        private readonly SqliteConnection _connection;
        private SqliteTransaction _transaction;

        public CommandDatabaseWriter(SqliteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void BeginTransaction()
        {
            _transaction = _connection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            _transaction?.Commit();
            _transaction?.Dispose();
            _transaction = null;
        }

        public void RollbackTransaction()
        {
            _transaction?.Rollback();
            _transaction?.Dispose();
            _transaction = null;
        }

        /// <summary>
        /// Inserts or retrieves a platform entry. Returns the platform row ID.
        /// </summary>
        public long EnsurePlatform(string edition, string version, string os)
        {
            using SqliteCommand select = _connection.CreateCommand();
            select.CommandText = "SELECT Id FROM Platform WHERE Edition = @e AND Version = @v AND OS = @o";
            select.Parameters.AddWithValue("@e", edition);
            select.Parameters.AddWithValue("@v", version);
            select.Parameters.AddWithValue("@o", os);
            select.Transaction = _transaction;

            object existing = select.ExecuteScalar();
            if (existing is not null)
            {
                return (long)existing;
            }

            using SqliteCommand insert = _connection.CreateCommand();
            insert.CommandText = "INSERT INTO Platform (Edition, Version, OS) VALUES (@e, @v, @o); SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@e", edition);
            insert.Parameters.AddWithValue("@v", version);
            insert.Parameters.AddWithValue("@o", os);
            insert.Transaction = _transaction;
            return (long)insert.ExecuteScalar();
        }

        /// <summary>
        /// Inserts or retrieves a module entry. Returns the module row ID.
        /// </summary>
        public long EnsureModule(string name, string version)
        {
            using SqliteCommand select = _connection.CreateCommand();
            select.CommandText = "SELECT Id FROM Module WHERE Name = @n AND (Version = @v OR (@v IS NULL AND Version IS NULL))";
            select.Parameters.AddWithValue("@n", name);
            select.Parameters.AddWithValue("@v", (object)version ?? DBNull.Value);
            select.Transaction = _transaction;

            object existing = select.ExecuteScalar();
            if (existing is not null)
            {
                return (long)existing;
            }

            using SqliteCommand insert = _connection.CreateCommand();
            insert.CommandText = "INSERT INTO Module (Name, Version) VALUES (@n, @v); SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@n", name);
            insert.Parameters.AddWithValue("@v", (object)version ?? DBNull.Value);
            insert.Transaction = _transaction;
            return (long)insert.ExecuteScalar();
        }

        /// <summary>
        /// Inserts a command. Returns the command row ID.
        /// </summary>
        public long InsertCommand(long moduleId, string name, string commandType, string defaultParameterSet)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Command (ModuleId, Name, CommandType, DefaultParameterSet)
                VALUES (@mid, @n, @ct, @dps);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@mid", moduleId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@ct", commandType);
            cmd.Parameters.AddWithValue("@dps", (object)defaultParameterSet ?? DBNull.Value);
            cmd.Transaction = _transaction;
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Links a command to a platform.
        /// </summary>
        public void LinkCommandPlatform(long commandId, long platformId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO CommandPlatform (CommandId, PlatformId) VALUES (@c, @p)";
            cmd.Parameters.AddWithValue("@c", commandId);
            cmd.Parameters.AddWithValue("@p", platformId);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts a parameter. Returns the parameter row ID.
        /// </summary>
        public long InsertParameter(long commandId, string name, string type, bool isDynamic)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Parameter (CommandId, Name, Type, IsDynamic)
                VALUES (@cid, @n, @t, @d);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@t", (object)type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", isDynamic ? 1 : 0);
            cmd.Transaction = _transaction;
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Links a parameter to a platform.
        /// </summary>
        public void LinkParameterPlatform(long parameterId, long platformId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO ParameterPlatform (ParameterId, PlatformId) VALUES (@p, @pl)";
            cmd.Parameters.AddWithValue("@p", parameterId);
            cmd.Parameters.AddWithValue("@pl", platformId);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts a parameter's membership in a parameter set.
        /// </summary>
        public void InsertParameterSetMembership(
            long parameterId,
            string setName,
            int? position,
            bool isMandatory,
            bool valueFromPipeline,
            bool valueFromPipelineByPropertyName)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO ParameterSetMembership
                    (ParameterId, SetName, Position, IsMandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)
                VALUES (@pid, @sn, @pos, @m, @vfp, @vfpbn)";
            cmd.Parameters.AddWithValue("@pid", parameterId);
            cmd.Parameters.AddWithValue("@sn", setName);
            cmd.Parameters.AddWithValue("@pos", (object)position ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@m", isMandatory ? 1 : 0);
            cmd.Parameters.AddWithValue("@vfp", valueFromPipeline ? 1 : 0);
            cmd.Parameters.AddWithValue("@vfpbn", valueFromPipelineByPropertyName ? 1 : 0);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts an alias for a command. Returns the alias row ID.
        /// </summary>
        public long InsertAlias(string aliasName, long commandId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Alias (Name, CommandId) VALUES (@n, @cid);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", aliasName);
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Transaction = _transaction;
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Links an alias to a platform.
        /// </summary>
        public void LinkAliasPlatform(long aliasId, long platformId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO AliasPlatform (AliasId, PlatformId) VALUES (@a, @p)";
            cmd.Parameters.AddWithValue("@a", aliasId);
            cmd.Parameters.AddWithValue("@p", platformId);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts an output type for a command.
        /// </summary>
        public void InsertOutputType(long commandId, string typeName)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO OutputType (CommandId, TypeName) VALUES (@cid, @t)";
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Parameters.AddWithValue("@t", typeName);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Inserts a type accelerator. Returns the row ID.
        /// </summary>
        public long InsertTypeAccelerator(string name, string fullName)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO TypeAccelerator (Name, FullName) VALUES (@n, @fn);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@fn", fullName);
            cmd.Transaction = _transaction;
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Links a type accelerator to a platform.
        /// </summary>
        public void LinkTypeAcceleratorPlatform(long typeAcceleratorId, long platformId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO TypeAcceleratorPlatform (TypeAcceleratorId, PlatformId) VALUES (@t, @p)";
            cmd.Parameters.AddWithValue("@t", typeAcceleratorId);
            cmd.Parameters.AddWithValue("@p", platformId);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Writes the schema version marker.
        /// </summary>
        public void WriteSchemaVersion(int version)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM SchemaVersion; INSERT INTO SchemaVersion (Version) VALUES (@v)";
            cmd.Parameters.AddWithValue("@v", version);
            cmd.Transaction = _transaction;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Tries to find an existing command by name within a module,
        /// so that platform links can be added to it without duplicating rows.
        /// Returns null if not found.
        /// </summary>
        public long? FindCommand(long moduleId, string name)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Command WHERE ModuleId = @mid AND Name = @n COLLATE NOCASE";
            cmd.Parameters.AddWithValue("@mid", moduleId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Transaction = _transaction;
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        /// <summary>
        /// Tries to find an existing alias by name targeting a specific command.
        /// Returns null if not found.
        /// </summary>
        public long? FindAlias(string aliasName, long commandId)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Alias WHERE Name = @n COLLATE NOCASE AND CommandId = @cid";
            cmd.Parameters.AddWithValue("@n", aliasName);
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Transaction = _transaction;
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        /// <summary>
        /// Tries to find an existing parameter by name on a command.
        /// Returns null if not found.
        /// </summary>
        public long? FindParameter(long commandId, string name)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Parameter WHERE CommandId = @cid AND Name = @n COLLATE NOCASE";
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Transaction = _transaction;
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        public void Dispose()
        {
            _transaction?.Dispose();
        }
    }
}
