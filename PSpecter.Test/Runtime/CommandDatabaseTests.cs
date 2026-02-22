using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Xunit;
using PSpecter.Runtime;

namespace PSpecter.Test.Runtime
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
        public void EnsurePlatform_InsertsAndRetrieves()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            long id1 = writer.EnsurePlatform("Core", "7.4.7", "windows");
            long id2 = writer.EnsurePlatform("Core", "7.4.7", "windows");

            Assert.Equal(id1, id2);
        }

        [Fact]
        public void InsertCommand_And_FindCommand()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            long modId = writer.EnsureModule("Microsoft.PowerShell.Management", "7.4.7");
            long cmdId = writer.InsertCommand(modId, "Get-ChildItem", "Cmdlet", null);

            long? found = writer.FindCommand(modId, "Get-ChildItem");
            Assert.NotNull(found);
            Assert.Equal(cmdId, found.Value);
        }

        [Fact]
        public void InsertParameter_And_ParameterSetMembership()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            long modId = writer.EnsureModule("TestModule", null);
            long cmdId = writer.InsertCommand(modId, "Test-Cmd", "Cmdlet", "Default");
            long paramId = writer.InsertParameter(cmdId, "Path", "System.String", false);

            writer.InsertParameterSetMembership(paramId, "Default", position: 0, isMandatory: true,
                valueFromPipeline: false, valueFromPipelineByPropertyName: true);
            writer.InsertParameterSetMembership(paramId, "LiteralPath", position: null, isMandatory: true,
                valueFromPipeline: false, valueFromPipelineByPropertyName: false);

            // Verify via direct query
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM ParameterSetMembership WHERE ParameterId = @pid";
            cmd.Parameters.AddWithValue("@pid", paramId);
            Assert.Equal(2L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void InsertAlias_And_FindAlias()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            long modId = writer.EnsureModule("Microsoft.PowerShell.Management", null);
            long cmdId = writer.InsertCommand(modId, "Get-ChildItem", "Cmdlet", null);
            long aliasId = writer.InsertAlias("gci", cmdId);

            long? found = writer.FindAlias("gci", cmdId);
            Assert.NotNull(found);
            Assert.Equal(aliasId, found.Value);
        }

        [Fact]
        public void Transaction_CommitsSuccessfully()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            writer.BeginTransaction();
            long modId = writer.EnsureModule("TxModule", "1.0");
            writer.InsertCommand(modId, "Test-Tx", "Function", null);
            writer.CommitTransaction();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Test-Tx'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void Transaction_RollbackDiscardsChanges()
        {
            using var connection = CreatePopulatedConnection();
            using var writer = new CommandDatabaseWriter(connection);

            writer.BeginTransaction();
            long modId = writer.EnsureModule("RbModule", "1.0");
            writer.InsertCommand(modId, "Test-Rb", "Function", null);
            writer.RollbackTransaction();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Test-Rb'";
            Assert.Equal(0L, (long)cmd.ExecuteScalar());
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

            // WindowsOnly-Cmd is only on windows
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
        public void GetAliasTarget_Delegates()
        {
            Assert.Equal("Get-ChildItem", _db.GetAliasTarget("gci"));
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
            // First call populates cache
            Assert.True(_db.TryGetCommand("Get-ChildItem", null, out CommandMetadata first));
            // Second call hits cache
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

        private static void PopulateTestDatabase(string dbPath)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            CommandDatabaseSchema.CreateTables(conn);

            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            writer.WriteSchemaVersion(CommandDatabaseSchema.SchemaVersion);

            long winPlatId = writer.EnsurePlatform("Core", "7.4.7", "windows");
            long macPlatId = writer.EnsurePlatform("Core", "7.4.7", "macos");

            // Get-ChildItem (cross-platform)
            long mgmtModId = writer.EnsureModule("Microsoft.PowerShell.Management", "7.4.7");
            long gciCmdId = writer.InsertCommand(mgmtModId, "Get-ChildItem", "Cmdlet", "Items");
            writer.LinkCommandPlatform(gciCmdId, winPlatId);
            writer.LinkCommandPlatform(gciCmdId, macPlatId);

            long pathParamId = writer.InsertParameter(gciCmdId, "Path", "System.String[]", false);
            writer.LinkParameterPlatform(pathParamId, winPlatId);
            writer.LinkParameterPlatform(pathParamId, macPlatId);
            writer.InsertParameterSetMembership(pathParamId, "Items", position: 0, isMandatory: false,
                valueFromPipeline: false, valueFromPipelineByPropertyName: true);

            long gciAliasId = writer.InsertAlias("gci", gciCmdId);
            writer.LinkAliasPlatform(gciAliasId, winPlatId);
            writer.LinkAliasPlatform(gciAliasId, macPlatId);

            long dirAliasId = writer.InsertAlias("dir", gciCmdId);
            writer.LinkAliasPlatform(dirAliasId, winPlatId);
            writer.LinkAliasPlatform(dirAliasId, macPlatId);

            writer.InsertOutputType(gciCmdId, "System.IO.FileInfo");
            writer.InsertOutputType(gciCmdId, "System.IO.DirectoryInfo");

            // WindowsOnly-Cmd (Windows only)
            long winModId = writer.EnsureModule("WinModule", "1.0");
            long winCmdId = writer.InsertCommand(winModId, "WindowsOnly-Cmd", "Cmdlet", null);
            writer.LinkCommandPlatform(winCmdId, winPlatId);

            long winAliasId = writer.InsertAlias("wincmd", winCmdId);
            writer.LinkAliasPlatform(winAliasId, winPlatId);

            writer.CommitTransaction();
        }
    }
}
