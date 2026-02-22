using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSpecter.Utils;

namespace PSpecter.CommandDatabase.Sqlite
{
    /// <summary>
    /// Read-only query layer over the PSpecter SQLite command database.
    /// Uses a segmented LRU cache so frequently-accessed commands stay hot.
    /// </summary>
    public sealed class SqliteCommandDatabase : IPowerShellCommandDatabase, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SegmentedLruCache<CacheKey, CommandMetadata> _cache;

        /// <param name="databasePath">Path to the SQLite .db file.</param>
        /// <param name="cacheCapacity">Total LRU cache capacity. Default 1024.</param>
        public SqliteCommandDatabase(string databasePath, int cacheCapacity = 1024)
        {
            if (databasePath is null) throw new ArgumentNullException(nameof(databasePath));

            _connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            _connection.Open();

            _cache = new SegmentedLruCache<CacheKey, CommandMetadata>(
                cacheCapacity,
                comparer: new CacheKeyComparer());
        }

        public bool TryGetCommand(string nameOrAlias, HashSet<PlatformInfo> platforms, out CommandMetadata command)
        {
            var key = new CacheKey(nameOrAlias, platforms);

            if (_cache.TryGet(key, out command))
            {
                return command is not null;
            }

            command = LoadCommand(nameOrAlias, platforms);
            _cache.Set(key, command);
            return command is not null;
        }

        public bool CommandExistsOnPlatform(string nameOrAlias, HashSet<PlatformInfo> platforms)
        {
            return TryGetCommand(nameOrAlias, platforms, out _);
        }

        public string GetAliasTarget(string alias)
        {
            if (TryGetCommand(alias, platforms: null, out CommandMetadata cmd))
            {
                return cmd.Name;
            }
            return null;
        }

        public IReadOnlyList<string> GetCommandAliases(string command)
        {
            if (TryGetCommand(command, platforms: null, out CommandMetadata cmd) && cmd.Aliases.Count > 0)
            {
                return cmd.Aliases;
            }
            return null;
        }

        public IReadOnlyList<string> GetAllNamesForCommand(string command)
        {
            if (TryGetCommand(command, platforms: null, out CommandMetadata cmd))
            {
                var names = new List<string>(1 + cmd.Aliases.Count) { cmd.Name };
                names.AddRange(cmd.Aliases);
                return names;
            }
            return null;
        }

        public void InvalidateCache()
        {
            _cache.Clear();
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        private CommandMetadata LoadCommand(string nameOrAlias, HashSet<PlatformInfo> platforms)
        {
            long? commandId = FindCommandId(nameOrAlias, platforms);
            if (commandId is null)
            {
                return null;
            }

            return BuildCommandMetadata(commandId.Value, platforms);
        }

        private long? FindCommandId(string nameOrAlias, HashSet<PlatformInfo> platforms)
        {
            string platformFilter = BuildPlatformFilter(platforms, out List<SqliteParameter> platParams);

            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                if (platformFilter is null)
                {
                    cmd.CommandText =
                        $"SELECT {Db.Command.Id} FROM {Db.Command.Table} " +
                        $"WHERE {Db.Command.Name} = @name COLLATE NOCASE LIMIT 1";
                }
                else
                {
                    cmd.CommandText =
                        $"SELECT c.{Db.Command.Id} FROM {Db.Command.Table} c " +
                        $"INNER JOIN {Db.CommandPlatform.Table} cp ON cp.{Db.CommandPlatform.CommandId} = c.{Db.Command.Id} " +
                        $"INNER JOIN {Db.Platform.Table} p ON p.{Db.Platform.Id} = cp.{Db.CommandPlatform.PlatformId} " +
                        $"WHERE c.{Db.Command.Name} = @name COLLATE NOCASE AND ({platformFilter}) LIMIT 1";
                    foreach (var p in platParams) cmd.Parameters.Add(p);
                }
                cmd.Parameters.AddWithValue("@name", nameOrAlias);

                object result = cmd.ExecuteScalar();
                if (result is not null)
                {
                    return (long)result;
                }
            }

            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                if (platformFilter is null)
                {
                    cmd.CommandText =
                        $"SELECT {Db.Alias.CommandId} FROM {Db.Alias.Table} " +
                        $"WHERE {Db.Alias.Name} = @name COLLATE NOCASE LIMIT 1";
                }
                else
                {
                    cmd.CommandText =
                        $"SELECT a.{Db.Alias.CommandId} FROM {Db.Alias.Table} a " +
                        $"INNER JOIN {Db.AliasPlatform.Table} ap ON ap.{Db.AliasPlatform.AliasId} = a.{Db.Alias.Id} " +
                        $"INNER JOIN {Db.Platform.Table} p ON p.{Db.Platform.Id} = ap.{Db.AliasPlatform.PlatformId} " +
                        $"WHERE a.{Db.Alias.Name} = @name COLLATE NOCASE AND ({platformFilter}) LIMIT 1";
                    foreach (var p in platParams) cmd.Parameters.Add(p);
                }
                cmd.Parameters.AddWithValue("@name", nameOrAlias);

                object result = cmd.ExecuteScalar();
                if (result is not null)
                {
                    return (long)result;
                }
            }

            return null;
        }

        private CommandMetadata BuildCommandMetadata(long commandId, HashSet<PlatformInfo> platforms)
        {
            string name = null;
            string commandType = null;
            string moduleName = null;
            string defaultParameterSet = null;

            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                cmd.CommandText =
                    $"SELECT c.{Db.Command.Name}, c.{Db.Command.CommandType}, c.{Db.Command.DefaultParameterSet}, m.{Db.Module.Name} " +
                    $"FROM {Db.Command.Table} c " +
                    $"LEFT JOIN {Db.Module.Table} m ON m.{Db.Module.Id} = c.{Db.Command.ModuleId} " +
                    $"WHERE c.{Db.Command.Id} = @id";
                cmd.Parameters.AddWithValue("@id", commandId);

                using SqliteDataReader reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;

                name = reader.GetString(0);
                commandType = reader.GetString(1);
                defaultParameterSet = reader.IsDBNull(2) ? null : reader.GetString(2);
                moduleName = reader.IsDBNull(3) ? null : reader.GetString(3);
            }

            var aliases = LoadAliases(commandId, platforms);
            var parameterSetNames = LoadParameterSetNames(commandId);
            var parameters = LoadParameters(commandId, platforms);
            var outputTypes = LoadOutputTypes(commandId);

            return new CommandMetadata(
                name,
                commandType,
                moduleName,
                defaultParameterSet,
                parameterSetNames,
                aliases,
                parameters,
                outputTypes);
        }

        private IReadOnlyList<string> LoadAliases(long commandId, HashSet<PlatformInfo> platforms)
        {
            string platformFilter = BuildPlatformFilter(platforms, out List<SqliteParameter> platParams);
            var aliases = new List<string>();

            using SqliteCommand cmd = _connection.CreateCommand();

            if (platformFilter is null)
            {
                cmd.CommandText =
                    $"SELECT {Db.Alias.Name} FROM {Db.Alias.Table} " +
                    $"WHERE {Db.Alias.CommandId} = @cid";
            }
            else
            {
                cmd.CommandText =
                    $"SELECT DISTINCT a.{Db.Alias.Name} FROM {Db.Alias.Table} a " +
                    $"INNER JOIN {Db.AliasPlatform.Table} ap ON ap.{Db.AliasPlatform.AliasId} = a.{Db.Alias.Id} " +
                    $"INNER JOIN {Db.Platform.Table} p ON p.{Db.Platform.Id} = ap.{Db.AliasPlatform.PlatformId} " +
                    $"WHERE a.{Db.Alias.CommandId} = @cid AND ({platformFilter})";
                foreach (var p in platParams) cmd.Parameters.Add(p);
            }
            cmd.Parameters.AddWithValue("@cid", commandId);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                aliases.Add(reader.GetString(0));
            }

            return aliases;
        }

        private IReadOnlyList<string> LoadParameterSetNames(long commandId)
        {
            var names = new List<string>();

            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"SELECT DISTINCT psm.{Db.ParameterSetMembership.SetName} " +
                $"FROM {Db.ParameterSetMembership.Table} psm " +
                $"INNER JOIN {Db.Parameter.Table} par ON par.{Db.Parameter.Id} = psm.{Db.ParameterSetMembership.ParameterId} " +
                $"WHERE par.{Db.Parameter.CommandId} = @cid " +
                $"ORDER BY psm.{Db.ParameterSetMembership.SetName}";
            cmd.Parameters.AddWithValue("@cid", commandId);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }

        private IReadOnlyList<ParameterMetadata> LoadParameters(long commandId, HashSet<PlatformInfo> platforms)
        {
            string platformFilter = BuildPlatformFilter(platforms, out List<SqliteParameter> platParams);
            var parameters = new List<ParameterMetadata>();

            List<(long Id, string Name, string Type, bool IsDynamic)> paramRows;

            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                if (platformFilter is null)
                {
                    cmd.CommandText =
                        $"SELECT {Db.Parameter.Id}, {Db.Parameter.Name}, {Db.Parameter.Type}, {Db.Parameter.IsDynamic} " +
                        $"FROM {Db.Parameter.Table} WHERE {Db.Parameter.CommandId} = @cid";
                }
                else
                {
                    cmd.CommandText =
                        $"SELECT DISTINCT par.{Db.Parameter.Id}, par.{Db.Parameter.Name}, par.{Db.Parameter.Type}, par.{Db.Parameter.IsDynamic} " +
                        $"FROM {Db.Parameter.Table} par " +
                        $"INNER JOIN {Db.ParameterPlatform.Table} pp ON pp.{Db.ParameterPlatform.ParameterId} = par.{Db.Parameter.Id} " +
                        $"INNER JOIN {Db.Platform.Table} p ON p.{Db.Platform.Id} = pp.{Db.ParameterPlatform.PlatformId} " +
                        $"WHERE par.{Db.Parameter.CommandId} = @cid AND ({platformFilter})";
                    foreach (var p in platParams) cmd.Parameters.Add(p);
                }
                cmd.Parameters.AddWithValue("@cid", commandId);

                paramRows = new List<(long, string, string, bool)>();
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    paramRows.Add((
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetInt64(3) != 0));
                }
            }

            foreach (var row in paramRows)
            {
                var sets = LoadParameterSetMemberships(row.Id);
                parameters.Add(new ParameterMetadata(row.Name, row.Type, row.IsDynamic, sets));
            }

            return parameters;
        }

        private IReadOnlyList<ParameterSetInfo> LoadParameterSetMemberships(long parameterId)
        {
            var sets = new List<ParameterSetInfo>();

            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"SELECT {Db.ParameterSetMembership.SetName}, {Db.ParameterSetMembership.Position}, " +
                $"{Db.ParameterSetMembership.IsMandatory}, {Db.ParameterSetMembership.ValueFromPipeline}, " +
                $"{Db.ParameterSetMembership.ValueFromPipelineByPropertyName} " +
                $"FROM {Db.ParameterSetMembership.Table} " +
                $"WHERE {Db.ParameterSetMembership.ParameterId} = @pid";
            cmd.Parameters.AddWithValue("@pid", parameterId);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                sets.Add(new ParameterSetInfo(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : (int)reader.GetInt64(1),
                    reader.GetInt64(2) != 0,
                    reader.GetInt64(3) != 0,
                    reader.GetInt64(4) != 0));
            }

            return sets;
        }

        private IReadOnlyList<string> LoadOutputTypes(long commandId)
        {
            var types = new List<string>();

            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"SELECT {Db.OutputType.TypeName} FROM {Db.OutputType.Table} " +
                $"WHERE {Db.OutputType.CommandId} = @cid";
            cmd.Parameters.AddWithValue("@cid", commandId);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                types.Add(reader.GetString(0));
            }

            return types;
        }

        private static string BuildPlatformFilter(HashSet<PlatformInfo> platforms, out List<SqliteParameter> parameters)
        {
            parameters = new List<SqliteParameter>();

            if (platforms is null || platforms.Count == 0)
            {
                return null;
            }

            var clauses = new List<string>();
            int i = 0;
            foreach (PlatformInfo plat in platforms)
            {
                string suffix = i.ToString();
                clauses.Add(
                    $"(p.{Db.Platform.Edition} = @e{suffix} AND " +
                    $"p.{Db.Platform.Version} = @v{suffix} AND " +
                    $"p.{Db.Platform.OS} = @o{suffix})");
                parameters.Add(new SqliteParameter($"@e{suffix}", plat.Edition));
                parameters.Add(new SqliteParameter($"@v{suffix}", plat.Version));
                parameters.Add(new SqliteParameter($"@o{suffix}", plat.OS));
                i++;
            }

            return string.Join(" OR ", clauses);
        }

        private readonly struct CacheKey
        {
            public readonly string NameOrAlias;
            public readonly HashSet<PlatformInfo> Platforms;

            public CacheKey(string nameOrAlias, HashSet<PlatformInfo> platforms)
            {
                NameOrAlias = nameOrAlias;
                Platforms = platforms;
            }
        }

        private sealed class CacheKeyComparer : IEqualityComparer<CacheKey>
        {
            public bool Equals(CacheKey x, CacheKey y)
            {
                if (!string.Equals(x.NameOrAlias, y.NameOrAlias, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (x.Platforms is null && y.Platforms is null) return true;
                if (x.Platforms is null || y.Platforms is null) return false;
                if (x.Platforms.Count != y.Platforms.Count) return false;

                foreach (PlatformInfo p in x.Platforms)
                {
                    if (!y.Platforms.Contains(p)) return false;
                }
                return true;
            }

            public int GetHashCode(CacheKey key)
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(key.NameOrAlias ?? string.Empty);
                if (key.Platforms is not null)
                {
                    foreach (PlatformInfo p in key.Platforms)
                    {
                        hash ^= p.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }
}
