using System;
using System.IO;
using Specter.Security;
using Xunit;

namespace Specter.Test.Security
{
    public class RulePathValidatorTests : IDisposable
    {
        private readonly string _tempDir;

        public RulePathValidatorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpecterRulePathTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void NullPath_IsRejected()
        {
            var result = RulePathValidator.ValidatePath(null!, allowedRoot: null, logger: null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void EmptyPath_IsRejected()
        {
            var result = RulePathValidator.ValidatePath("", allowedRoot: null, logger: null);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void PathWithNullByte_IsRejected()
        {
            var result = RulePathValidator.ValidatePath("/some/path\0evil", allowedRoot: null, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("null bytes", result.RejectionReason);
        }

        [Fact]
        public void TraversalOutsideAllowedRoot_IsRejected()
        {
            string subDir = Path.Combine(_tempDir, "rules");
            Directory.CreateDirectory(subDir);
            string filePath = Path.Combine(_tempDir, "rules", "..", "..", "evil.dll");

            var result = RulePathValidator.ValidatePath(filePath, allowedRoot: subDir, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("outside the allowed root", result.RejectionReason);
        }

        [Fact]
        public void NonexistentPath_IsRejected()
        {
            string fakePath = Path.Combine(_tempDir, "nonexistent.dll");
            var result = RulePathValidator.ValidatePath(fakePath, allowedRoot: null, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("does not exist", result.RejectionReason);
        }

        [Fact]
        public void UnsupportedExtension_IsRejected()
        {
            string txtFile = Path.Combine(_tempDir, "rules.txt");
            File.WriteAllText(txtFile, "not a rule");

            var result = RulePathValidator.ValidatePath(txtFile, allowedRoot: null, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("unsupported extension", result.RejectionReason);
        }

        [Fact]
        public void DllFile_ClassifiedAsAssembly()
        {
            string dllFile = Path.Combine(_tempDir, "MyRules.dll");
            File.WriteAllText(dllFile, "fake");

            var result = RulePathValidator.ValidatePath(dllFile, allowedRoot: null, logger: null);
            Assert.True(result.IsValid);
            Assert.Equal(RuleFileKind.Assembly, result.Kind);
        }

        [Fact]
        public void Psm1File_ClassifiedAsScriptModule()
        {
            string psm1File = Path.Combine(_tempDir, "MyRules.psm1");
            File.WriteAllText(psm1File, "function Measure-Foo {}");

            var result = RulePathValidator.ValidatePath(psm1File, allowedRoot: null, logger: null);
            Assert.True(result.IsValid);
            Assert.Equal(RuleFileKind.ScriptModule, result.Kind);
        }

        [Fact]
        public void Psd1File_ClassifiedAsModuleManifest()
        {
            string psd1File = Path.Combine(_tempDir, "MyRules.psd1");
            File.WriteAllText(psd1File, "@{ RootModule = 'MyRules.psm1' }");

            var result = RulePathValidator.ValidatePath(psd1File, allowedRoot: null, logger: null);
            Assert.True(result.IsValid);
            Assert.Equal(RuleFileKind.ModuleManifest, result.Kind);
        }

        [Fact]
        public void Directory_ClassifiedAsDirectory()
        {
            var result = RulePathValidator.ValidatePath(_tempDir, allowedRoot: null, logger: null);
            Assert.True(result.IsValid);
            Assert.Equal(RuleFileKind.Directory, result.Kind);
        }

        [Fact]
        public void PathWithinAllowedRoot_Accepted()
        {
            string dllFile = Path.Combine(_tempDir, "Good.dll");
            File.WriteAllText(dllFile, "fake");

            var result = RulePathValidator.ValidatePath(dllFile, allowedRoot: _tempDir, logger: null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ClassifyByExtension_Dll_IsAssembly()
        {
            Assert.Equal(RuleFileKind.Assembly, RulePathValidator.ClassifyByExtension("test.dll"));
        }

        [Fact]
        public void ClassifyByExtension_Psm1_IsScriptModule()
        {
            Assert.Equal(RuleFileKind.ScriptModule, RulePathValidator.ClassifyByExtension("test.psm1"));
        }

        [Fact]
        public void ClassifyByExtension_Psd1_IsModuleManifest()
        {
            Assert.Equal(RuleFileKind.ModuleManifest, RulePathValidator.ClassifyByExtension("test.psd1"));
        }

        [Fact]
        public void ClassifyByExtension_Txt_IsUnknown()
        {
            Assert.Equal(RuleFileKind.Unknown, RulePathValidator.ClassifyByExtension("test.txt"));
        }

        [Fact]
        public void ClassifyByExtension_NoExtension_IsUnknown()
        {
            Assert.Equal(RuleFileKind.Unknown, RulePathValidator.ClassifyByExtension("test"));
        }
    }
}
