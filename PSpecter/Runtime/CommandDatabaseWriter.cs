using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PSpecter.Runtime
{
    /// <summary>
    /// Bulk-inserts command metadata into the PSpecter SQLite database.
    /// Accepts high-level <see cref="CommandMetadata"/> objects and decomposes
    /// them into relational rows using prepared statements for performance.
    /// </summary>
    public sealed class CommandDatabaseWriter : IDisposable
    {
        private readonly SqliteConnection _connection;

        // Caches for deduplication within a batch
        private readonly Dictionary<(string, string, string), long> _platformCache
            = new Dictionary<(string, string, string), long>();
        private readonly Dictionary<(string, string), long> _moduleCache
            = new Dictionary<(string, string), long>();

        public CommandDatabaseWriter(SqliteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public DatabaseTransactionScope BeginTransaction()
        {
            return new DatabaseTransactionScope(_connection);
        }

        /// <summary>
        /// Imports a batch of commands for a given platform. All inserts happen
        /// inside the provided transaction scope.
        /// </summary>
        public void ImportCommands(
            IReadOnlyList<CommandMetadata> commands,
            PlatformInfo platform,
            DatabaseTransactionScope tx)
        {
            if (commands is null) throw new ArgumentNullException(nameof(commands));
            if (platform is null) throw new ArgumentNullException(nameof(platform));
            if (tx is null) throw new ArgumentNullException(nameof(tx));

            SqliteTransaction transaction = tx.Transaction;
            long platformId = EnsurePlatform(platform.Edition, platform.Version, platform.OS, transaction);

            using var insertCommand = PrepareInsertCommand(transaction);
            using var linkCommandPlat = PrepareLinkCommandPlatform(transaction);
            using var insertParam = PrepareInsertParameter(transaction);
            using var linkParamPlat = PrepareLinkParameterPlatform(transaction);
            using var insertPsm = PrepareInsertParameterSetMembership(transaction);
            using var insertAlias = PrepareInsertAlias(transaction);
            using var linkAliasPlat = PrepareLinkAliasPlatform(transaction);
            using var insertOutput = PrepareInsertOutputType(transaction);

            foreach (CommandMetadata meta in commands)
            {
                long moduleId = EnsureModule(meta.ModuleName, moduleVersion: null, transaction);

                long? existingId = FindCommand(moduleId, meta.Name, transaction);
                long commandId = existingId
                    ?? ExecuteInsertCommand(insertCommand, moduleId, meta.Name, meta.CommandType, meta.DefaultParameterSet);

                ExecuteLink(linkCommandPlat, commandId, platformId);

                foreach (ParameterMetadata param in meta.Parameters)
                {
                    long? existingParamId = FindParameter(commandId, param.Name, transaction);
                    long paramId = existingParamId
                        ?? ExecuteInsertParameter(insertParam, commandId, param.Name, param.Type, param.IsDynamic);

                    ExecuteLink(linkParamPlat, paramId, platformId);

                    foreach (ParameterSetInfo psi in param.ParameterSets)
                    {
                        ExecuteInsertPsm(insertPsm, paramId, psi);
                    }
                }

                foreach (string alias in meta.Aliases)
                {
                    long? existingAlias = FindAlias(alias, commandId, transaction);
                    long aliasId = existingAlias
                        ?? ExecuteInsertAlias(insertAlias, alias, commandId);

                    ExecuteLink(linkAliasPlat, aliasId, platformId);
                }

                foreach (string outputType in meta.OutputTypes)
                {
                    ExecuteInsertOutputType(insertOutput, commandId, outputType);
                }
            }
        }

        /// <summary>
        /// Writes the schema version marker.
        /// </summary>
        public void WriteSchemaVersion(int version, SqliteTransaction transaction = null)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"DELETE FROM {Db.SchemaVersionTable.Table}; " +
                $"INSERT INTO {Db.SchemaVersionTable.Table} ({Db.SchemaVersionTable.Version}) VALUES (@v)";
            cmd.Parameters.AddWithValue("@v", version);
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            // Writer doesn't own the connection
        }

        // ---------- EnsurePlatform / EnsureModule with in-process caching ----------

        public long EnsurePlatform(string edition, string version, string os, SqliteTransaction transaction)
        {
            var key = (edition, version, os);
            if (_platformCache.TryGetValue(key, out long cached))
                return cached;

            using SqliteCommand select = _connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText =
                $"SELECT {Db.Platform.Id} FROM {Db.Platform.Table} " +
                $"WHERE {Db.Platform.Edition} = @e AND {Db.Platform.Version} = @v AND {Db.Platform.OS} = @o";
            select.Parameters.AddWithValue("@e", edition);
            select.Parameters.AddWithValue("@v", version);
            select.Parameters.AddWithValue("@o", os);

            object existing = select.ExecuteScalar();
            if (existing is not null)
            {
                long id = (long)existing;
                _platformCache[key] = id;
                return id;
            }

            using SqliteCommand insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                $"INSERT INTO {Db.Platform.Table} ({Db.Platform.Edition}, {Db.Platform.Version}, {Db.Platform.OS}) " +
                $"VALUES (@e, @v, @o); SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@e", edition);
            insert.Parameters.AddWithValue("@v", version);
            insert.Parameters.AddWithValue("@o", os);

            long newId = (long)insert.ExecuteScalar();
            _platformCache[key] = newId;
            return newId;
        }

        public long EnsureModule(string name, string moduleVersion, SqliteTransaction transaction)
        {
            string normVersion = moduleVersion ?? string.Empty;
            var key = (name, normVersion);
            if (_moduleCache.TryGetValue(key, out long cached))
                return cached;

            using SqliteCommand select = _connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText =
                $"SELECT {Db.Module.Id} FROM {Db.Module.Table} " +
                $"WHERE {Db.Module.Name} = @n AND ({Db.Module.Version} = @v OR (@v = '' AND {Db.Module.Version} IS NULL))";
            select.Parameters.AddWithValue("@n", name);
            select.Parameters.AddWithValue("@v", normVersion);

            object existing = select.ExecuteScalar();
            if (existing is not null)
            {
                long id = (long)existing;
                _moduleCache[key] = id;
                return id;
            }

            using SqliteCommand insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                $"INSERT INTO {Db.Module.Table} ({Db.Module.Name}, {Db.Module.Version}) " +
                $"VALUES (@n, @v); SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@n", name);
            insert.Parameters.AddWithValue("@v", string.IsNullOrEmpty(moduleVersion) ? (object)DBNull.Value : moduleVersion);

            long newId = (long)insert.ExecuteScalar();
            _moduleCache[key] = newId;
            return newId;
        }

        // ---------- Find helpers (not cached – rare within a batch) ----------

        public long? FindCommand(long moduleId, string name, SqliteTransaction transaction)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"SELECT {Db.Command.Id} FROM {Db.Command.Table} " +
                $"WHERE {Db.Command.ModuleId} = @mid AND {Db.Command.Name} = @n COLLATE NOCASE";
            cmd.Parameters.AddWithValue("@mid", moduleId);
            cmd.Parameters.AddWithValue("@n", name);
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        public long? FindParameter(long commandId, string name, SqliteTransaction transaction)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"SELECT {Db.Parameter.Id} FROM {Db.Parameter.Table} " +
                $"WHERE {Db.Parameter.CommandId} = @cid AND {Db.Parameter.Name} = @n COLLATE NOCASE";
            cmd.Parameters.AddWithValue("@cid", commandId);
            cmd.Parameters.AddWithValue("@n", name);
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        public long? FindAlias(string aliasName, long commandId, SqliteTransaction transaction)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"SELECT {Db.Alias.Id} FROM {Db.Alias.Table} " +
                $"WHERE {Db.Alias.Name} = @n COLLATE NOCASE AND {Db.Alias.CommandId} = @cid";
            cmd.Parameters.AddWithValue("@n", aliasName);
            cmd.Parameters.AddWithValue("@cid", commandId);
            object result = cmd.ExecuteScalar();
            return result is null ? null : (long)result;
        }

        // ---------- Prepared statement factories ----------

        private SqliteCommand PrepareInsertCommand(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT INTO {Db.Command.Table} " +
                $"({Db.Command.ModuleId}, {Db.Command.Name}, {Db.Command.CommandType}, {Db.Command.DefaultParameterSet}) " +
                $"VALUES (@mid, @n, @ct, @dps); SELECT last_insert_rowid();";
            cmd.Parameters.Add(new SqliteParameter("@mid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@ct", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@dps", SqliteType.Text));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkCommandPlatform(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.CommandPlatform.Table} " +
                $"({Db.CommandPlatform.CommandId}, {Db.CommandPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareInsertParameter(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT INTO {Db.Parameter.Table} " +
                $"({Db.Parameter.CommandId}, {Db.Parameter.Name}, {Db.Parameter.Type}, {Db.Parameter.IsDynamic}) " +
                $"VALUES (@cid, @n, @t, @d); SELECT last_insert_rowid();";
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@t", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@d", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkParameterPlatform(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.ParameterPlatform.Table} " +
                $"({Db.ParameterPlatform.ParameterId}, {Db.ParameterPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareInsertParameterSetMembership(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.ParameterSetMembership.Table} " +
                $"({Db.ParameterSetMembership.ParameterId}, {Db.ParameterSetMembership.SetName}, " +
                $"{Db.ParameterSetMembership.Position}, {Db.ParameterSetMembership.IsMandatory}, " +
                $"{Db.ParameterSetMembership.ValueFromPipeline}, {Db.ParameterSetMembership.ValueFromPipelineByPropertyName}) " +
                $"VALUES (@pid, @sn, @pos, @m, @vfp, @vfpbn)";
            cmd.Parameters.Add(new SqliteParameter("@pid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@sn", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@pos", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@m", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@vfp", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@vfpbn", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareInsertAlias(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT INTO {Db.Alias.Table} ({Db.Alias.Name}, {Db.Alias.CommandId}) " +
                $"VALUES (@n, @cid); SELECT last_insert_rowid();";
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkAliasPlatform(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.AliasPlatform.Table} " +
                $"({Db.AliasPlatform.AliasId}, {Db.AliasPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareInsertOutputType(SqliteTransaction tx)
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                $"INSERT INTO {Db.OutputType.Table} ({Db.OutputType.CommandId}, {Db.OutputType.TypeName}) " +
                $"VALUES (@cid, @t)";
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@t", SqliteType.Text));
            cmd.Prepare();
            return cmd;
        }

        // ---------- Prepared statement executors ----------

        private static long ExecuteInsertCommand(SqliteCommand prepared, long moduleId, string name, string commandType, string defaultParameterSet)
        {
            prepared.Parameters["@mid"].Value = moduleId;
            prepared.Parameters["@n"].Value = name;
            prepared.Parameters["@ct"].Value = (object)commandType ?? "Cmdlet";
            prepared.Parameters["@dps"].Value = (object)defaultParameterSet ?? DBNull.Value;
            return (long)prepared.ExecuteScalar();
        }

        private static void ExecuteLink(SqliteCommand prepared, long id1, long id2)
        {
            prepared.Parameters["@a"].Value = id1;
            prepared.Parameters["@b"].Value = id2;
            prepared.ExecuteNonQuery();
        }

        private static long ExecuteInsertParameter(SqliteCommand prepared, long commandId, string name, string type, bool isDynamic)
        {
            prepared.Parameters["@cid"].Value = commandId;
            prepared.Parameters["@n"].Value = name;
            prepared.Parameters["@t"].Value = (object)type ?? DBNull.Value;
            prepared.Parameters["@d"].Value = isDynamic ? 1 : 0;
            return (long)prepared.ExecuteScalar();
        }

        private static void ExecuteInsertPsm(SqliteCommand prepared, long parameterId, ParameterSetInfo psi)
        {
            prepared.Parameters["@pid"].Value = parameterId;
            prepared.Parameters["@sn"].Value = psi.SetName;
            prepared.Parameters["@pos"].Value = (object)psi.Position ?? DBNull.Value;
            prepared.Parameters["@m"].Value = psi.IsMandatory ? 1 : 0;
            prepared.Parameters["@vfp"].Value = psi.ValueFromPipeline ? 1 : 0;
            prepared.Parameters["@vfpbn"].Value = psi.ValueFromPipelineByPropertyName ? 1 : 0;
            prepared.ExecuteNonQuery();
        }

        private static long ExecuteInsertAlias(SqliteCommand prepared, string aliasName, long commandId)
        {
            prepared.Parameters["@n"].Value = aliasName;
            prepared.Parameters["@cid"].Value = commandId;
            return (long)prepared.ExecuteScalar();
        }

        private static void ExecuteInsertOutputType(SqliteCommand prepared, long commandId, string typeName)
        {
            prepared.Parameters["@cid"].Value = commandId;
            prepared.Parameters["@t"].Value = typeName;
            prepared.ExecuteNonQuery();
        }
    }
}
