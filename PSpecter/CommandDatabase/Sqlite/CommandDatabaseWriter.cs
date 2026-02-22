using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PSpecter.CommandDatabase.Sqlite
{
    /// <summary>
    /// Bulk-inserts command metadata into the PSpecter SQLite database.
    /// Each instance encapsulates a single transaction. Create via
    /// <see cref="Begin"/>; call <see cref="Commit"/> when done.
    /// If disposed without committing, the transaction is rolled back.
    /// </summary>
    public sealed class CommandDatabaseWriter : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _transaction;
        private bool _committed;

        private readonly Dictionary<(string, string, string), long> _platformCache
            = new Dictionary<(string, string, string), long>();
        private readonly Dictionary<(string, string), long> _moduleCache
            = new Dictionary<(string, string), long>();

        private CommandDatabaseWriter(SqliteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transaction = connection.BeginTransaction();
        }

        /// <summary>
        /// Opens a transactional writer on the given connection.
        /// The caller must dispose the returned writer when done.
        /// </summary>
        public static CommandDatabaseWriter Begin(SqliteConnection connection)
        {
            return new CommandDatabaseWriter(connection);
        }

        /// <summary>
        /// Commits the transaction. Must be called explicitly;
        /// otherwise <see cref="Dispose"/> will roll back.
        /// </summary>
        public void Commit()
        {
            _transaction.Commit();
            _committed = true;
        }

        /// <summary>
        /// Imports a batch of commands for a given platform.
        /// </summary>
        public void ImportCommands(
            IReadOnlyList<CommandMetadata> commands,
            PlatformInfo platform)
        {
            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (platform is null)
            {
                throw new ArgumentNullException(nameof(platform));
            }

            long platformId = EnsurePlatform(platform.Edition, platform.Version, platform.OS);

            using var upsertCommand = PrepareUpsertCommand();
            using var linkCommandPlat = PrepareLinkCommandPlatform();
            using var upsertParam = PrepareUpsertParameter();
            using var linkParamPlat = PrepareLinkParameterPlatform();
            using var upsertPsm = PrepareUpsertParameterSetMembership();
            using var upsertAlias = PrepareUpsertAlias();
            using var linkAliasPlat = PrepareLinkAliasPlatform();
            using var upsertOutput = PrepareUpsertOutputType();

            foreach (CommandMetadata meta in commands)
            {
                long moduleId = EnsureModule(meta.ModuleName ?? string.Empty, moduleVersion: null);

                long commandId = ExecuteUpsertCommand(upsertCommand, moduleId, meta.Name, meta.CommandType, meta.DefaultParameterSet);
                ExecuteLink(linkCommandPlat, commandId, platformId);

                foreach (ParameterMetadata param in meta.Parameters)
                {
                    long paramId = ExecuteUpsertParameter(upsertParam, commandId, param.Name, param.Type, param.IsDynamic);
                    ExecuteLink(linkParamPlat, paramId, platformId);

                    foreach (ParameterSetInfo psi in param.ParameterSets)
                    {
                        ExecuteUpsertPsm(upsertPsm, paramId, psi);
                    }
                }

                foreach (string alias in meta.Aliases)
                {
                    long aliasId = ExecuteUpsertAlias(upsertAlias, alias, commandId);
                    ExecuteLink(linkAliasPlat, aliasId, platformId);
                }

                foreach (string outputType in meta.OutputTypes)
                {
                    ExecuteUpsertOutputType(upsertOutput, commandId, outputType);
                }
            }
        }

        /// <summary>
        /// Writes the schema version marker.
        /// </summary>
        public void WriteSchemaVersion(int version)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"DELETE FROM {Db.SchemaVersionTable.Table}; " +
                $"INSERT INTO {Db.SchemaVersionTable.Table} ({Db.SchemaVersionTable.Version}) VALUES (@v)";
            cmd.Parameters.AddWithValue("@v", version);
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (!_committed)
            {
                try { _transaction.Rollback(); } catch { }
            }
            _transaction.Dispose();
        }

        // ---------- EnsurePlatform / EnsureModule with upsert + in-process caching ----------

        private long EnsurePlatform(string edition, string version, string os)
        {
            var key = (edition, version, os);
            if (_platformCache.TryGetValue(key, out long cached))
            {
                return cached;
            }

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT INTO {Db.Platform.Table} ({Db.Platform.Edition}, {Db.Platform.Version}, {Db.Platform.OS}) " +
                $"VALUES (@e, @v, @o) " +
                $"ON CONFLICT({Db.Platform.Edition}, {Db.Platform.Version}, {Db.Platform.OS}) " +
                $"DO UPDATE SET {Db.Platform.Edition} = {Db.Platform.Edition} " +
                $"RETURNING {Db.Platform.Id}";
            cmd.Parameters.AddWithValue("@e", edition);
            cmd.Parameters.AddWithValue("@v", version);
            cmd.Parameters.AddWithValue("@o", os);

            long id = (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("Expected platform ID from INSERT"));
            _platformCache[key] = id;
            return id;
        }

        private long EnsureModule(string name, string? moduleVersion)
        {
            string normName = name ?? string.Empty;
            string normVersion = moduleVersion ?? string.Empty;
            var key = (normName, normVersion);
            if (_moduleCache.TryGetValue(key, out long cached))
            {
                return cached;
            }

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT INTO {Db.Module.Table} ({Db.Module.Name}, {Db.Module.Version}) " +
                $"VALUES (@n, @v) " +
                $"ON CONFLICT({Db.Module.Name}, {Db.Module.Version}) " +
                $"DO UPDATE SET {Db.Module.Name} = {Db.Module.Name} " +
                $"RETURNING {Db.Module.Id}";
            cmd.Parameters.AddWithValue("@n", normName);
            cmd.Parameters.AddWithValue("@v", normVersion);

            long id = (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("Expected module ID from INSERT"));
            _moduleCache[key] = id;
            return id;
        }

        // ---------- Prepared upsert statement factories ----------

        private SqliteCommand PrepareUpsertCommand()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT INTO {Db.Command.Table} " +
                $"({Db.Command.ModuleId}, {Db.Command.Name}, {Db.Command.CommandType}, {Db.Command.DefaultParameterSet}) " +
                $"VALUES (@mid, @n, @ct, @dps) " +
                $"ON CONFLICT({Db.Command.ModuleId}, {Db.Command.Name}) " +
                $"DO UPDATE SET {Db.Command.Name} = {Db.Command.Name} " +
                $"RETURNING {Db.Command.Id}";
            cmd.Parameters.Add(new SqliteParameter("@mid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@ct", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@dps", SqliteType.Text));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkCommandPlatform()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.CommandPlatform.Table} " +
                $"({Db.CommandPlatform.CommandId}, {Db.CommandPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareUpsertParameter()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT INTO {Db.Parameter.Table} " +
                $"({Db.Parameter.CommandId}, {Db.Parameter.Name}, {Db.Parameter.Type}, {Db.Parameter.IsDynamic}) " +
                $"VALUES (@cid, @n, @t, @d) " +
                $"ON CONFLICT({Db.Parameter.CommandId}, {Db.Parameter.Name}) " +
                $"DO UPDATE SET {Db.Parameter.Name} = {Db.Parameter.Name} " +
                $"RETURNING {Db.Parameter.Id}";
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@t", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@d", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkParameterPlatform()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.ParameterPlatform.Table} " +
                $"({Db.ParameterPlatform.ParameterId}, {Db.ParameterPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareUpsertParameterSetMembership()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
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

        private SqliteCommand PrepareUpsertAlias()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT INTO {Db.Alias.Table} ({Db.Alias.Name}, {Db.Alias.CommandId}) " +
                $"VALUES (@n, @cid) " +
                $"ON CONFLICT({Db.Alias.Name}, {Db.Alias.CommandId}) " +
                $"DO UPDATE SET {Db.Alias.Name} = {Db.Alias.Name} " +
                $"RETURNING {Db.Alias.Id}";
            cmd.Parameters.Add(new SqliteParameter("@n", SqliteType.Text));
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareLinkAliasPlatform()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.AliasPlatform.Table} " +
                $"({Db.AliasPlatform.AliasId}, {Db.AliasPlatform.PlatformId}) VALUES (@a, @b)";
            cmd.Parameters.Add(new SqliteParameter("@a", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@b", SqliteType.Integer));
            cmd.Prepare();
            return cmd;
        }

        private SqliteCommand PrepareUpsertOutputType()
        {
            var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText =
                $"INSERT OR IGNORE INTO {Db.OutputType.Table} " +
                $"({Db.OutputType.CommandId}, {Db.OutputType.TypeName}) " +
                $"VALUES (@cid, @t)";
            cmd.Parameters.Add(new SqliteParameter("@cid", SqliteType.Integer));
            cmd.Parameters.Add(new SqliteParameter("@t", SqliteType.Text));
            cmd.Prepare();
            return cmd;
        }

        // ---------- Prepared statement executors ----------

        private static long ExecuteUpsertCommand(SqliteCommand prepared, long moduleId, string name, string commandType, string? defaultParameterSet)
        {
            prepared.Parameters["@mid"].Value = moduleId;
            prepared.Parameters["@n"].Value = name;
            prepared.Parameters["@ct"].Value = commandType ?? (object)"Cmdlet";
            prepared.Parameters["@dps"].Value = defaultParameterSet ?? (object)DBNull.Value;
            return (long)(prepared.ExecuteScalar() ?? throw new InvalidOperationException("Expected command ID from upsert"));
        }

        private static void ExecuteLink(SqliteCommand prepared, long id1, long id2)
        {
            prepared.Parameters["@a"].Value = id1;
            prepared.Parameters["@b"].Value = id2;
            prepared.ExecuteNonQuery();
        }

        private static long ExecuteUpsertParameter(SqliteCommand prepared, long commandId, string name, string? type, bool isDynamic)
        {
            prepared.Parameters["@cid"].Value = commandId;
            prepared.Parameters["@n"].Value = name;
            prepared.Parameters["@t"].Value = type ?? (object)DBNull.Value;
            prepared.Parameters["@d"].Value = isDynamic ? 1 : 0;
            return (long)(prepared.ExecuteScalar() ?? throw new InvalidOperationException("Expected parameter ID from upsert"));
        }

        private static void ExecuteUpsertPsm(SqliteCommand prepared, long parameterId, ParameterSetInfo psi)
        {
            prepared.Parameters["@pid"].Value = parameterId;
            prepared.Parameters["@sn"].Value = psi.SetName;
            prepared.Parameters["@pos"].Value = psi.Position ?? (object)DBNull.Value;
            prepared.Parameters["@m"].Value = psi.IsMandatory ? 1 : 0;
            prepared.Parameters["@vfp"].Value = psi.ValueFromPipeline ? 1 : 0;
            prepared.Parameters["@vfpbn"].Value = psi.ValueFromPipelineByPropertyName ? 1 : 0;
            prepared.ExecuteNonQuery();
        }

        private static long ExecuteUpsertAlias(SqliteCommand prepared, string aliasName, long commandId)
        {
            prepared.Parameters["@n"].Value = aliasName;
            prepared.Parameters["@cid"].Value = commandId;
            return (long)(prepared.ExecuteScalar() ?? throw new InvalidOperationException("Expected alias ID from upsert"));
        }

        private static void ExecuteUpsertOutputType(SqliteCommand prepared, long commandId, string? typeName)
        {
            prepared.Parameters["@cid"].Value = commandId;
            prepared.Parameters["@t"].Value = typeName ?? (object)DBNull.Value;
            prepared.ExecuteNonQuery();
        }
    }
}
