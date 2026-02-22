using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PSpecter.Utils;

namespace PSpecter.Runtime
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

        /// <summary>
        /// Looks up a command by canonical name or alias, optionally filtered to
        /// a set of platforms. Returns true if found.
        /// </summary>
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

        /// <summary>
        /// Checks whether a command (name or alias) exists for the given platforms.
        /// </summary>
        public bool CommandExistsOnPlatform(string nameOrAlias, HashSet<PlatformInfo> platforms)
        {
            return TryGetCommand(nameOrAlias, platforms, out _);
        }

        // --- Legacy IPowerShellCommandDatabase members ---

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

        /// <summary>
        /// Finds a command ID by looking first in the Command table, then the Alias table.
        /// If platforms is non-empty, restricts to commands available on those platforms.
        /// </summary>
        private long? FindCommandId(string nameOrAlias, HashSet<PlatformInfo> platforms)
        {
            string platformFilter = BuildPlatformFilter(platforms, out List<SqliteParameter> platParams);

            // Try direct command name first
            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                if (platformFilter is null)
                {
                    cmd.CommandText = "SELECT Id FROM Command WHERE Name = @name COLLATE NOCASE LIMIT 1";
                }
                else
                {
                    cmd.CommandText = $@"
                        SELECT c.Id FROM Command c
                        INNER JOIN CommandPlatform cp ON cp.CommandId = c.Id
                        INNER JOIN Platform p ON p.Id = cp.PlatformId
                        WHERE c.Name = @name COLLATE NOCASE AND ({platformFilter})
                        LIMIT 1";
                    foreach (var p in platParams) cmd.Parameters.Add(p);
                }
                cmd.Parameters.AddWithValue("@name", nameOrAlias);

                object result = cmd.ExecuteScalar();
                if (result is not null)
                {
                    return (long)result;
                }
            }

            // Try alias lookup
            using (SqliteCommand cmd = _connection.CreateCommand())
            {
                if (platformFilter is null)
                {
                    cmd.CommandText = "SELECT CommandId FROM Alias WHERE Name = @name COLLATE NOCASE LIMIT 1";
                }
                else
                {
                    cmd.CommandText = $@"
                        SELECT a.CommandId FROM Alias a
                        INNER JOIN AliasPlatform ap ON ap.AliasId = a.Id
                        INNER JOIN Platform p ON p.Id = ap.PlatformId
                        WHERE a.Name = @name COLLATE NOCASE AND ({platformFilter})
                        LIMIT 1";
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
                cmd.CommandText = @"
                    SELECT c.Name, c.CommandType, c.DefaultParameterSet, m.Name
                    FROM Command c
                    LEFT JOIN Module m ON m.Id = c.ModuleId
                    WHERE c.Id = @id";
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
                cmd.CommandText = "SELECT Name FROM Alias WHERE CommandId = @cid";
            }
            else
            {
                cmd.CommandText = $@"
                    SELECT DISTINCT a.Name FROM Alias a
                    INNER JOIN AliasPlatform ap ON ap.AliasId = a.Id
                    INNER JOIN Platform p ON p.Id = ap.PlatformId
                    WHERE a.CommandId = @cid AND ({platformFilter})";
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
            cmd.CommandText = @"
                SELECT DISTINCT psm.SetName
                FROM ParameterSetMembership psm
                INNER JOIN Parameter par ON par.Id = psm.ParameterId
                WHERE par.CommandId = @cid
                ORDER BY psm.SetName";
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
                    cmd.CommandText = "SELECT Id, Name, Type, IsDynamic FROM Parameter WHERE CommandId = @cid";
                }
                else
                {
                    cmd.CommandText = $@"
                        SELECT DISTINCT par.Id, par.Name, par.Type, par.IsDynamic
                        FROM Parameter par
                        INNER JOIN ParameterPlatform pp ON pp.ParameterId = par.Id
                        INNER JOIN Platform p ON p.Id = pp.PlatformId
                        WHERE par.CommandId = @cid AND ({platformFilter})";
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
            cmd.CommandText = @"
                SELECT SetName, Position, IsMandatory, ValueFromPipeline, ValueFromPipelineByPropertyName
                FROM ParameterSetMembership
                WHERE ParameterId = @pid";
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
            cmd.CommandText = "SELECT TypeName FROM OutputType WHERE CommandId = @cid";
            cmd.Parameters.AddWithValue("@cid", commandId);

            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                types.Add(reader.GetString(0));
            }

            return types;
        }

        /// <summary>
        /// Builds a SQL filter expression like "(p.Edition = @e0 AND p.Version = @v0 AND p.OS = @o0) OR ..."
        /// for a set of platform filters. Returns null if no filter is needed.
        /// </summary>
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
                clauses.Add($"(p.Edition = @e{suffix} AND p.Version = @v{suffix} AND p.OS = @o{suffix})");
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
