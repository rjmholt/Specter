using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Xunit;
using PSpecter.CommandDatabase;
using PSpecter.CommandDatabase.Sqlite;

namespace PSpecter.Test.CommandDatabase
{
    public class CommandDatabaseSchemaTests
    {
        [Fact]
        public void CreateTables_Succeeds_OnInMemoryDatabase()
        {
            using var connection = CreateInMemoryConnection();
            CommandDatabaseSchema.CreateTables(connection);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            var tables = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));

            Assert.Contains("Platform", tables);
            Assert.Contains("Module", tables);
            Assert.Contains("Command", tables);
            Assert.Contains("CommandPlatform", tables);
            Assert.Contains("Parameter", tables);
            Assert.Contains("ParameterPlatform", tables);
            Assert.Contains("ParameterSetMembership", tables);
            Assert.Contains("Alias", tables);
            Assert.Contains("AliasPlatform", tables);
            Assert.Contains("OutputType", tables);
            Assert.Contains("TypeAccelerator", tables);
            Assert.Contains("TypeAcceleratorPlatform", tables);
            Assert.Contains("SchemaVersion", tables);
        }

        [Fact]
        public void CreateTables_IsIdempotent()
        {
            using var connection = CreateInMemoryConnection();
            CommandDatabaseSchema.CreateTables(connection);
            CommandDatabaseSchema.CreateTables(connection);
        }

        private static SqliteConnection CreateInMemoryConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            return conn;
        }
    }

    public class CommandDatabaseWriterTests
    {
        [Fact]
        public void ImportCommands_InsertsAndDeduplicatesPlatforms()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);

            var platform = new PlatformInfo("Core", "7.4.7", "windows");
            var commands = new[]
            {
                new CommandMetadata("Get-Foo", "Cmdlet", "TestModule", null, null, null, null, null),
            };

            writer.ImportCommands(commands, platform);
            writer.ImportCommands(commands, platform);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Platform WHERE Edition='Core' AND Version='7.4.7' AND OS='windows'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportCommands_InsertsCommandWithParametersAndAliases()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);

            var platform = new PlatformInfo("Core", "7.4.7", "windows");
            var commands = new[]
            {
                new CommandMetadata(
                    "Get-ChildItem",
                    "Cmdlet",
                    "Microsoft.PowerShell.Management",
                    "Items",
                    null,
                    new[] { "gci", "dir" },
                    new[]
                    {
                        new ParameterMetadata("Path", "System.String[]", false, new[]
                        {
                            new ParameterSetInfo("Items", 0, false, false, true),
                        }),
                    },
                    new[] { "System.IO.FileInfo" }),
            };

            writer.ImportCommands(commands, platform);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Get-ChildItem'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM Alias WHERE Name = 'gci'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM Alias WHERE Name = 'dir'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM Parameter WHERE Name = 'Path'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM OutputType WHERE TypeName = 'System.IO.FileInfo'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportCommands_DeduplicatesCommandsAcrossPlatforms()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);

            var winPlatform = new PlatformInfo("Core", "7.4.7", "windows");
            var macPlatform = new PlatformInfo("Core", "7.4.7", "macos");

            var commands = new[]
            {
                new CommandMetadata("Get-ChildItem", "Cmdlet", "Microsoft.PowerShell.Management", null, null, null, null, null),
            };

            writer.ImportCommands(commands, winPlatform);
            writer.ImportCommands(commands, macPlatform);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Get-ChildItem'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM CommandPlatform WHERE CommandId = (SELECT Id FROM Command WHERE Name = 'Get-ChildItem')";
            Assert.Equal(2L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void Transaction_RollsBackOnDispose()
        {
            using var connection = CreatePopulatedConnection();

            using (var writer = CommandDatabaseWriter.Begin(connection))
            {
                var platform = new PlatformInfo("Core", "7.0.0", "windows");
                var commands = new[]
                {
                    new CommandMetadata("Test-Rollback", "Function", "TestModule", null, null, null, null, null),
                };
                writer.ImportCommands(commands, platform);
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Test-Rollback'";
            Assert.Equal(0L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportCommands_NullModuleName_StoredAsEmptyString()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);

            var platform = new PlatformInfo("Core", "7.4.7", "windows");
            var commands = new[]
            {
                new CommandMetadata("Test-NoModule", "Function", null, null, null, null, null, null),
            };

            writer.ImportCommands(commands, platform);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT m.Name FROM Module m
                INNER JOIN Command c ON c.ModuleId = m.Id
                WHERE c.Name = 'Test-NoModule'";
            Assert.Equal("", (string)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportCommands_NullModuleName_DeduplicatesCorrectly()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);

            var platform1 = new PlatformInfo("Core", "7.4.7", "windows");
            var platform2 = new PlatformInfo("Core", "7.4.7", "macos");

            var commands = new[]
            {
                new CommandMetadata("Test-NoModule", "Function", null, null, null, null, null, null),
            };

            writer.ImportCommands(commands, platform1);
            writer.ImportCommands(commands, platform2);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Test-NoModule'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = @"
                SELECT COUNT(*) FROM CommandPlatform
                WHERE CommandId = (SELECT Id FROM Command WHERE Name = 'Test-NoModule')";
            Assert.Equal(2L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void WriteSchemaVersion_Works()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = CommandDatabaseWriter.Begin(connection);
            writer.WriteSchemaVersion(42);
            writer.Commit();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Version FROM SchemaVersion";
            Assert.Equal(42L, (long)cmd.ExecuteScalar());
        }

        private static SqliteConnection CreatePopulatedConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            CommandDatabaseSchema.CreateTables(conn);
            return conn;
        }
    }

    public class SqliteCommandDatabaseTests : System.IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteCommandDatabase _db;

        public SqliteCommandDatabaseTests()
        {
            _dbPath = System.IO.Path.GetTempFileName();
            PopulateTestDatabase(_dbPath);
            _db = new SqliteCommandDatabase(_dbPath);
        }

        public void Dispose()
        {
            _db.Dispose();
            try { System.IO.File.Delete(_dbPath); } catch { }
        }

        [Fact]
        public void TryGetCommand_FindsByCanonicalName()
        {
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata cmd));
            Assert.Equal("Get-ChildItem", cmd.Name);
            Assert.Equal("Cmdlet", cmd.CommandType);
            Assert.Equal("Microsoft.PowerShell.Management", cmd.ModuleName);
        }

        [Fact]
        public void TryGetCommand_FindsByAlias()
        {
            Assert.True(_db.TryGetCommand("gci", null, out CommandMetadata cmd));
            Assert.Equal("Get-ChildItem", cmd.Name);
            Assert.Contains("gci", cmd.Aliases);
        }

        [Fact]
        public void TryGetCommand_ReturnsFalseForMissing()
        {
            Assert.False(_db.TryGetCommand("Not-A-Command", null, out _));
        }

        [Fact]
        public void TryGetCommand_CaseInsensitive()
        {
            Assert.True(_db.TryGetCommand("get-childitem", null, out CommandMetadata cmd));
            Assert.Equal("Get-ChildItem", cmd.Name);
        }

        [Fact]
        public void TryGetCommand_ReturnsParameters()
        {
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata cmd));
            Assert.True(cmd.Parameters.Count > 0);

            ParameterMetadata pathParam = null;
            foreach (var p in cmd.Parameters)
            {
                if (p.Name == "Path") { pathParam = p; break; }
            }

            Assert.NotNull(pathParam);
            Assert.Equal("System.String[]", pathParam.Type);
            Assert.True(pathParam.ParameterSets.Count > 0);

            var setInfo = pathParam.ParameterSets[0];
            Assert.Equal("Items", setInfo.SetName);
            Assert.Equal(0, setInfo.Position);
            Assert.False(setInfo.IsMandatory);
            Assert.True(setInfo.ValueFromPipelineByPropertyName);
        }

        [Fact]
        public void TryGetCommand_ReturnsOutputTypes()
        {
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata cmd));
            Assert.Contains("System.IO.FileInfo", cmd.OutputTypes);
        }

        [Fact]
        public void TryGetCommand_PlatformFiltered()
        {
            var windowsPlatforms = new HashSet<PlatformInfo>
            {
                new PlatformInfo("Core", "7.4.7", "windows")
            };
            var macPlatforms = new HashSet<PlatformInfo>
            {
                new PlatformInfo("Core", "7.4.7", "macos")
            };

            Assert.True(_db.TryGetCommand("WindowsOnly-Cmd", windowsPlatforms, out _));
            Assert.False(_db.TryGetCommand("WindowsOnly-Cmd", macPlatforms, out _));
        }

        [Fact]
        public void TryGetCommand_PlatformFilteredAlias()
        {
            var windowsPlatforms = new HashSet<PlatformInfo>
            {
                new PlatformInfo("Core", "7.4.7", "windows")
            };
            var macPlatforms = new HashSet<PlatformInfo>
            {
                new PlatformInfo("Core", "7.4.7", "macos")
            };

            Assert.True(_db.TryGetCommand("wincmd", windowsPlatforms, out _));
            Assert.False(_db.TryGetCommand("wincmd", macPlatforms, out _));
        }

        [Fact]
        public void GetAliasTarget_ReturnsTargetForAlias()
        {
            Assert.Equal("Get-ChildItem", _db.GetAliasTarget("gci"));
        }

        [Fact]
        public void GetAliasTarget_ReturnsNullForCanonicalName()
        {
            Assert.Null(_db.GetAliasTarget("Get-ChildItem"));
        }

        [Fact]
        public void GetAliasTarget_ReturnsNullForCanonicalName_CaseInsensitive()
        {
            Assert.Null(_db.GetAliasTarget("get-childitem"));
        }

        [Fact]
        public void GetCommandAliases_Delegates()
        {
            IReadOnlyList<string> aliases = _db.GetCommandAliases("Get-ChildItem");
            Assert.NotNull(aliases);
            Assert.Contains("gci", aliases);
            Assert.Contains("dir", aliases);
        }

        [Fact]
        public void GetAllNamesForCommand_Delegates()
        {
            IReadOnlyList<string> names = _db.GetAllNamesForCommand("Get-ChildItem");
            Assert.NotNull(names);
            Assert.Contains("Get-ChildItem", names);
            Assert.Contains("gci", names);
        }

        [Fact]
        public void CommandExistsOnPlatform_Works()
        {
            Assert.True(_db.CommandExistsOnPlatform("Get-ChildItem", null));
            Assert.False(_db.CommandExistsOnPlatform("Not-Real", null));
        }

        [Fact]
        public void CachingReturnsConsistentResults()
        {
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata first));
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata second));

            Assert.Equal(first.Name, second.Name);
            Assert.Equal(first.CommandType, second.CommandType);
        }

        [Fact]
        public void InvalidateCache_ForcesReload()
        {
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out _));
            _db.InvalidateCache();
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out _));
        }

        [Fact]
        public void TryGetCommand_NullModuleName_ReturnsEmptyModuleName()
        {
            Assert.True(_db.TryGetCommand("NoModule-Cmd", null, out CommandMetadata cmd));
            Assert.Equal("", cmd.ModuleName);
        }

        [Fact]
        public void TryGetCommand_NullModuleName_FindsByAlias()
        {
            Assert.True(_db.TryGetCommand("nmc", null, out CommandMetadata cmd));
            Assert.Equal("NoModule-Cmd", cmd.Name);
        }

        [Fact]
        public void GetAliasTarget_ReturnsNullForUnknownCommand()
        {
            Assert.Null(_db.GetAliasTarget("nonexistent-alias"));
        }

        private static void PopulateTestDatabase(string dbPath)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            CommandDatabaseSchema.CreateTables(conn);

            using var writer = CommandDatabaseWriter.Begin(conn);
            writer.WriteSchemaVersion(CommandDatabaseSchema.SchemaVersion);

            var winPlatform = new PlatformInfo("Core", "7.4.7", "windows");
            var macPlatform = new PlatformInfo("Core", "7.4.7", "macos");

            var crossPlatCommands = new[]
            {
                new CommandMetadata(
                    "Get-ChildItem",
                    "Cmdlet",
                    "Microsoft.PowerShell.Management",
                    "Items",
                    null,
                    new[] { "gci", "dir" },
                    new[]
                    {
                        new ParameterMetadata("Path", "System.String[]", false, new[]
                        {
                            new ParameterSetInfo("Items", 0, false, false, true),
                        }),
                    },
                    new[] { "System.IO.FileInfo", "System.IO.DirectoryInfo" }),
            };

            writer.ImportCommands(crossPlatCommands, winPlatform);
            writer.ImportCommands(crossPlatCommands, macPlatform);

            var winOnlyCommands = new[]
            {
                new CommandMetadata(
                    "WindowsOnly-Cmd",
                    "Cmdlet",
                    "WinModule",
                    null,
                    null,
                    new[] { "wincmd" },
                    null,
                    null),
            };

            writer.ImportCommands(winOnlyCommands, winPlatform);

            var noModuleCommands = new[]
            {
                new CommandMetadata(
                    "NoModule-Cmd",
                    "Function",
                    null,
                    null,
                    null,
                    new[] { "nmc" },
                    null,
                    null),
            };

            writer.ImportCommands(noModuleCommands, winPlatform);

            writer.Commit();
        }
    }
}
