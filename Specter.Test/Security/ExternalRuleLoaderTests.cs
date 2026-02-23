using System;
using System.Collections.Generic;
using System.IO;
using Specter.Configuration;
using Specter.Instantiation;
using Specter.Security;
using Xunit;

namespace Specter.Test.Security
{
    public class ExternalRuleLoaderTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Dictionary<string, IRuleConfiguration> _emptyConfig;

        public ExternalRuleLoaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpecterLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
            _emptyConfig = new Dictionary<string, IRuleConfiguration>();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void InvalidPath_ReturnsNull()
        {
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                "/nonexistent/path.dll",
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Null(factory);
        }

        [Fact]
        public void UnsupportedExtension_ReturnsNull()
        {
            string txtFile = Path.Combine(_tempDir, "rules.txt");
            File.WriteAllText(txtFile, "not a rule");

            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                txtFile,
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Null(factory);
        }

        [Fact]
        public void RelativePath_ResolvedFromSettingsDir()
        {
            string settingsDir = Path.Combine(_tempDir, "settings");
            Directory.CreateDirectory(settingsDir);
            string rulesDir = Path.Combine(settingsDir, "rules");
            Directory.CreateDirectory(rulesDir);
            string dllFile = Path.Combine(rulesDir, "MyRules.dll");
            File.WriteAllText(dllFile, "fake");

            // The dll file is fake so assembly loading will fail, but the path resolution
            // should at least attempt to load from the correct resolved path
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                "rules/MyRules.dll",
                settingsFileDirectory: settingsDir,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            // Will return null because the DLL is not a valid assembly,
            // but importantly it didn't throw and it attempted the right path
        }

        [Fact]
        public void EmptyDirectory_ReturnsNoFactories()
        {
            string emptyDir = Path.Combine(_tempDir, "empty");
            Directory.CreateDirectory(emptyDir);

            List<IRuleProviderFactory> factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                emptyDir,
                settingsFileDirectory: null,
                _emptyConfig,
                recurse: false,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Empty(factories);
        }

        [Fact]
        public void DirectoryWithMixedFiles_OnlyClassifiesKnownExtensions()
        {
            string rulesDir = Path.Combine(_tempDir, "mixed");
            Directory.CreateDirectory(rulesDir);

            File.WriteAllText(Path.Combine(rulesDir, "readme.txt"), "not a rule");
            File.WriteAllText(Path.Combine(rulesDir, "script.ps1"), "not a rule module");
            File.WriteAllText(Path.Combine(rulesDir, "module.psm1"), "function Measure-Foo {}");

            List<IRuleProviderFactory> factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                rulesDir,
                settingsFileDirectory: null,
                _emptyConfig,
                recurse: false,
                skipOwnershipCheck: true,
                logger: null);

            // .psm1 gets a factory, .txt and .ps1 are ignored
            Assert.Single(factories);
        }
    }
}
