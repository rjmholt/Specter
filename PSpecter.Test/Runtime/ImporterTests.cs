using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Xunit;
using PSpecter.Runtime;
using PSpecter.Runtime.Import;

namespace PSpecter.Test.Runtime
{
    public class LegacySettingsImporterTests
    {
        [Theory]
        [InlineData("core-6.1.0-windows", "Core", "6.1.0", "windows")]
        [InlineData("desktop-5.1.14393.206-windows", "Desktop", "5.1.14393.206", "windows")]
        [InlineData("core-6.1.0-macos", "Core", "6.1.0", "macos")]
        [InlineData("core-6.1.0-linux", "Core", "6.1.0", "linux")]
        [InlineData("core-6.1.0-linux-arm", "Core", "6.1.0-linux", "arm")]
        public void TryParsePlatformFromFileName_ParsesCorrectly(
            string fileName, string expectedEdition, string expectedVersion, string expectedOs)
        {
            Assert.True(LegacySettingsImporter.TryParsePlatformFromFileName(
                fileName, out string edition, out string version, out string os));
            Assert.Equal(expectedEdition, edition);
            Assert.Equal(expectedVersion, version);
            Assert.Equal(expectedOs, os);
        }

        [Theory]
        [InlineData("")]
        [InlineData("nodashes")]
        [InlineData("only-onedash")]
        public void TryParsePlatformFromFileName_ReturnsFalseForInvalid(string fileName)
        {
            Assert.False(LegacySettingsImporter.TryParsePlatformFromFileName(
                fileName, out _, out _, out _));
        }

        [Fact]
        public void ImportJson_ImportsModulesAndCommands()
        {
            string json = @"{
                ""SchemaVersion"": ""0.0.1"",
                ""Modules"": [
                    {
                        ""Name"": ""TestModule"",
                        ""Version"": ""1.0.0"",
                        ""ExportedCommands"": [
                            { ""Name"": ""Get-Thing"", ""CommandType"": ""Cmdlet"" },
                            { ""Name"": ""Set-Thing"", ""CommandType"": ""Cmdlet"" }
                        ],
                        ""ExportedAliases"": [""gt""]
                    }
                ]
            }";

            using var conn = CreateConnection();
            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            long platformId = writer.EnsurePlatform("Core", "7.0.0", "windows");
            LegacySettingsImporter.ImportJson(writer, json, platformId);
            writer.CommitTransaction();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Get-Thing'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Set-Thing'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM Module WHERE Name = 'TestModule'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportJson_DeduplicatesCommandsAcrossPlatforms()
        {
            string json = @"{
                ""SchemaVersion"": ""0.0.1"",
                ""Modules"": [
                    {
                        ""Name"": ""Shared"",
                        ""Version"": ""1.0"",
                        ""ExportedCommands"": [
                            { ""Name"": ""Get-Cross"", ""CommandType"": ""Cmdlet"" }
                        ],
                        ""ExportedAliases"": []
                    }
                ]
            }";

            using var conn = CreateConnection();
            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            long winId = writer.EnsurePlatform("Core", "7.0.0", "windows");
            long macId = writer.EnsurePlatform("Core", "7.0.0", "macos");
            LegacySettingsImporter.ImportJson(writer, json, winId);
            LegacySettingsImporter.ImportJson(writer, json, macId);
            writer.CommitTransaction();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Get-Cross'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM CommandPlatform WHERE CommandId = (SELECT Id FROM Command WHERE Name = 'Get-Cross')";
            Assert.Equal(2L, (long)cmd.ExecuteScalar());
        }

        private static SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            CommandDatabaseSchema.CreateTables(conn);
            return conn;
        }
    }

    public class CompatibilityProfileImporterTests
    {
        [Fact]
        public void ImportJson_ImportsCmdletsWithParameters()
        {
            string json = @"{
                ""Platform"": {
                    ""PowerShell"": {
                        ""Version"": { ""Major"": 7, ""Minor"": 4, ""Patch"": 7 },
                        ""Edition"": ""Core""
                    },
                    ""OperatingSystem"": {
                        ""Family"": ""Windows""
                    }
                },
                ""Runtime"": {
                    ""Modules"": {
                        ""TestModule"": {
                            ""1.0.0"": {
                                ""Guid"": ""00000000-0000-0000-0000-000000000000"",
                                ""Cmdlets"": {
                                    ""Get-Widget"": {
                                        ""OutputType"": [""System.String""],
                                        ""ParameterSets"": [""Default"", ""ById""],
                                        ""DefaultParameterSet"": ""Default"",
                                        ""Parameters"": {
                                            ""Name"": {
                                                ""Type"": ""System.String"",
                                                ""Dynamic"": false,
                                                ""ParameterSets"": {
                                                    ""Default"": {
                                                        ""Flags"": [""Mandatory"", ""ValueFromPipelineByPropertyName""],
                                                        ""Position"": 0
                                                    }
                                                }
                                            },
                                            ""Id"": {
                                                ""Type"": ""System.Int32"",
                                                ""Dynamic"": false,
                                                ""ParameterSets"": {
                                                    ""ById"": {
                                                        ""Flags"": [""Mandatory""],
                                                        ""Position"": 0
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                ""Functions"": {},
                                ""Aliases"": {
                                    ""gw"": ""Get-Widget""
                                }
                            }
                        }
                    }
                }
            }";

            using var conn = CreateConnection();
            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            CompatibilityProfileImporter.ImportJson(writer, json);
            writer.CommitTransaction();

            // Verify platform
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Platform WHERE Edition='Core' AND Version='7.4.7' AND OS='windows'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());

            // Verify command
            cmd.CommandText = "SELECT Id FROM Command WHERE Name = 'Get-Widget'";
            long commandId = (long)cmd.ExecuteScalar();
            Assert.True(commandId > 0);

            // Verify parameters
            cmd.CommandText = "SELECT COUNT(*) FROM Parameter WHERE CommandId = @cid";
            cmd.Parameters.AddWithValue("@cid", commandId);
            Assert.Equal(2L, (long)cmd.ExecuteScalar());

            // Verify parameter set membership
            cmd.Parameters.Clear();
            cmd.CommandText = @"
                SELECT psm.SetName, psm.IsMandatory, psm.Position, psm.ValueFromPipelineByPropertyName
                FROM ParameterSetMembership psm
                INNER JOIN Parameter p ON p.Id = psm.ParameterId
                WHERE p.CommandId = @cid AND p.Name = 'Name'";
            cmd.Parameters.AddWithValue("@cid", commandId);
            using (var reader = cmd.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal("Default", reader.GetString(0));
                Assert.Equal(1L, reader.GetInt64(1)); // IsMandatory
                Assert.Equal(0L, reader.GetInt64(2)); // Position
                Assert.Equal(1L, reader.GetInt64(3)); // ValueFromPipelineByPropertyName
            }

            // Verify alias
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT CommandId FROM Alias WHERE Name = 'gw'";
            long aliasTarget = (long)cmd.ExecuteScalar();
            Assert.Equal(commandId, aliasTarget);

            // Verify output type
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT TypeName FROM OutputType WHERE CommandId = @cid";
            cmd.Parameters.AddWithValue("@cid", commandId);
            Assert.Equal("System.String", (string)cmd.ExecuteScalar());
        }

        [Fact]
        public void ImportJson_ImportsFunctions()
        {
            string json = @"{
                ""Platform"": {
                    ""PowerShell"": { ""Version"": ""5.1.17763"", ""Edition"": ""Desktop"" },
                    ""OperatingSystem"": { ""Family"": ""Windows"" }
                },
                ""Runtime"": {
                    ""Modules"": {
                        ""MyModule"": {
                            ""2.0.0"": {
                                ""Cmdlets"": {},
                                ""Functions"": {
                                    ""Invoke-MyFunc"": {
                                        ""Parameters"": {
                                            ""Input"": {
                                                ""Type"": ""System.Object"",
                                                ""Dynamic"": false,
                                                ""ParameterSets"": {
                                                    ""__AllParameterSets"": {
                                                        ""Flags"": [""ValueFromPipeline""],
                                                        ""Position"": -2147483648
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                ""Aliases"": {}
                            }
                        }
                    }
                }
            }";

            using var conn = CreateConnection();
            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            CompatibilityProfileImporter.ImportJson(writer, json);
            writer.CommitTransaction();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CommandType FROM Command WHERE Name = 'Invoke-MyFunc'";
            Assert.Equal("Function", (string)cmd.ExecuteScalar());

            // Position = int.MinValue should be stored as null
            cmd.CommandText = @"
                SELECT psm.Position
                FROM ParameterSetMembership psm
                INNER JOIN Parameter p ON p.Id = psm.ParameterId
                WHERE p.Name = 'Input'";
            Assert.True(cmd.ExecuteScalar() is System.DBNull);
        }

        [Fact]
        public void ImportJson_HandlesMissingOptionalFields()
        {
            string json = @"{
                ""Runtime"": {
                    ""Modules"": {
                        ""MinimalModule"": {
                            ""1.0"": {
                                ""Cmdlets"": {
                                    ""Test-Minimal"": {}
                                }
                            }
                        }
                    }
                }
            }";

            using var conn = CreateConnection();
            using var writer = new CommandDatabaseWriter(conn);
            writer.BeginTransaction();
            CompatibilityProfileImporter.ImportJson(writer, json);
            writer.CommitTransaction();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Command WHERE Name = 'Test-Minimal'";
            Assert.Equal(1L, (long)cmd.ExecuteScalar());
        }

        private static SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            CommandDatabaseSchema.CreateTables(conn);
            return conn;
        }
    }
}
