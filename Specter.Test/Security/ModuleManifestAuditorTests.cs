using System;
using System.IO;
using Specter.Security;
using Xunit;

namespace Specter.Test.Security
{
    public class ModuleManifestAuditorTests : IDisposable
    {
        private readonly string _tempDir;

        public ModuleManifestAuditorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpecterManifestTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void CleanManifest_Passes()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; ModuleVersion = '1.0.0' }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ScriptsToProcess_IsRejected()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; ScriptsToProcess = @('evil.ps1') }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("ScriptsToProcess", result.RejectionReason);
        }

        [Fact]
        public void TypesToProcess_IsRejected()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; TypesToProcess = @('types.ps1xml') }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("TypesToProcess", result.RejectionReason);
        }

        [Fact]
        public void FormatsToProcess_IsRejected()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; FormatsToProcess = @('format.ps1xml') }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("FormatsToProcess", result.RejectionReason);
        }

        [Fact]
        public void RequiredAssemblies_IsRejected()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; RequiredAssemblies = @('evil.dll') }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("RequiredAssemblies", result.RejectionReason);
        }

        [Fact]
        public void EmptyScriptsToProcess_Passes()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; ScriptsToProcess = @() }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void NestedModulesInsideModuleDir_Passes()
        {
            string subDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; NestedModules = @('sub/helper.psm1') }");

            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void NestedModulesOutsideModuleDir_IsRejected()
        {
            string manifest = WriteManifest("@{ RootModule = 'MyRules.psm1'; NestedModules = @('../evil/helper.psm1') }");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("NestedModules", result.RejectionReason);
            Assert.Contains("outside the module directory", result.RejectionReason);
        }

        [Fact]
        public void NonexistentManifest_IsRejected()
        {
            var result = ModuleManifestAuditor.Audit(Path.Combine(_tempDir, "nope.psd1"), logger: null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void MalformedManifest_IsRejected()
        {
            string manifest = WriteManifest("this is not a hashtable");
            var result = ModuleManifestAuditor.Audit(manifest, logger: null);
            Assert.False(result.IsValid);
        }

        private string WriteManifest(string content)
        {
            string path = Path.Combine(_tempDir, "Test.psd1");
            File.WriteAllText(path, content);
            return path;
        }
    }
}
