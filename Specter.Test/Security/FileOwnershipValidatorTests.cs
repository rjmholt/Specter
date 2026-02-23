using System;
using System.IO;
using System.Runtime.InteropServices;
using Specter.Security;
using Xunit;

#pragma warning disable CA1416 // Test methods are guarded by [FactOnUnix]

namespace Specter.Test.Security
{
    public class FileOwnershipValidatorTests : IDisposable
    {
        private readonly string _tempDir;

        public FileOwnershipValidatorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpecterOwnerTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void CurrentUserOwnedFile_Passes()
        {
            string file = Path.Combine(_tempDir, "good.dll");
            File.WriteAllText(file, "content");

            var result = FileOwnershipValidator.ValidateSingle(file, logger: null);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateFileAndParents_StopsAtRoot()
        {
            string subDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);
            string file = Path.Combine(subDir, "rule.psm1");
            File.WriteAllText(file, "content");

            var result = FileOwnershipValidator.ValidateFileAndParents(
                file,
                stopAtRoot: _tempDir,
                logger: null);

            Assert.True(result.IsValid);
        }

        [FactOnUnix]
        public void GroupWritableFile_IsRejectedOnUnix()
        {
            string file = Path.Combine(_tempDir, "groupwrite.dll");
            File.WriteAllText(file, "content");

            File.SetUnixFileMode(file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.OtherRead);

            var result = FileOwnershipValidator.ValidateSingle(file, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("group-writable", result.RejectionReason);
        }

        [FactOnUnix]
        public void WorldWritableFile_IsRejectedOnUnix()
        {
            string file = Path.Combine(_tempDir, "worldwrite.dll");
            File.WriteAllText(file, "content");

            File.SetUnixFileMode(file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite);

            var result = FileOwnershipValidator.ValidateSingle(file, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("world-writable", result.RejectionReason);
        }

        [FactOnUnix]
        public void WorldWritableParentDir_RejectsChild()
        {
            string subDir = Path.Combine(_tempDir, "writable_dir");
            Directory.CreateDirectory(subDir);
            string file = Path.Combine(subDir, "rule.dll");
            File.WriteAllText(file, "content");

            File.SetUnixFileMode(subDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);

            var result = FileOwnershipValidator.ValidateFileAndParents(
                file,
                stopAtRoot: _tempDir,
                logger: null);

            Assert.False(result.IsValid);
        }
    }

    public sealed class FactOnUnixAttribute : FactAttribute
    {
        public FactOnUnixAttribute()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "Unix-only test";
            }
        }
    }
}
