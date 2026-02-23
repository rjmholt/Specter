using System;
using System.IO;
using System.Runtime.InteropServices;
#if CORECLR
using System.Runtime.Versioning;
#endif
using Specter.Logging;

#if CORECLR
using System.Security.Principal;
using System.Security.AccessControl;
#endif

namespace Specter.Security
{
    internal readonly struct OwnershipCheckResult
    {
        internal OwnershipCheckResult(bool isValid, string? rejectionReason = null)
        {
            IsValid = isValid;
            RejectionReason = rejectionReason;
        }

        internal bool IsValid { get; }
        internal string? RejectionReason { get; }
    }

    internal static class FileOwnershipValidator
    {
        internal static OwnershipCheckResult ValidateFileAndParents(
            string canonicalPath,
            string? stopAtRoot,
            IAnalysisLogger? logger)
        {
            OwnershipCheckResult result = ValidateSingle(canonicalPath, logger);
            if (!result.IsValid)
            {
                return result;
            }

            string? parent = Path.GetDirectoryName(canonicalPath);
            string? normalizedStop = stopAtRoot is not null
                ? Path.GetFullPath(stopAtRoot)
                : null;

            while (parent is not null)
            {
                if (normalizedStop is not null
                    && parent.Equals(normalizedStop, PathComparison))
                {
                    break;
                }

                result = ValidateSingle(parent, logger);
                if (!result.IsValid)
                {
                    return result;
                }

                string? grandparent = Path.GetDirectoryName(parent);
                if (grandparent == parent)
                {
                    break;
                }

                parent = grandparent;
            }

            return new OwnershipCheckResult(true);
        }

        internal static OwnershipCheckResult ValidateSingle(string path, IAnalysisLogger? logger)
        {
#if CORECLR
            if (OperatingSystem.IsWindows())
            {
                return ValidateWindows(path, logger);
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return ValidateUnix(path, logger);
            }

            logger?.Warning($"Ownership check skipped for '{path}': unsupported platform.");
            return new OwnershipCheckResult(true);
#else
            // net462 is Windows-only (Windows PowerShell)
            return ValidateWindows(path, logger);
#endif
        }

#if CORECLR
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
#endif
        private static OwnershipCheckResult ValidateUnix(string path, IAnalysisLogger? logger)
        {
#if CORECLR
            try
            {
                var mode = System.IO.File.GetUnixFileMode(path);
                if ((mode & UnixFileMode.GroupWrite) != 0)
                {
                    string msg = $"'{path}' is group-writable (mode {mode}). Set permissions to remove group write access.";
                    logger?.Warning(msg);
                    return new OwnershipCheckResult(false, msg);
                }

                if ((mode & UnixFileMode.OtherWrite) != 0)
                {
                    string msg = $"'{path}' is world-writable (mode {mode}). Set permissions to remove other write access.";
                    logger?.Warning(msg);
                    return new OwnershipCheckResult(false, msg);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning($"Could not check permissions for '{path}': {ex.Message}");
                return new OwnershipCheckResult(true);
            }

            if (!IsOwnedByCurrentUserOrRoot(path, logger))
            {
                string msg = $"'{path}' is not owned by the current user or root.";
                logger?.Warning(msg);
                return new OwnershipCheckResult(false, msg);
            }
#endif

            return new OwnershipCheckResult(true);
        }

        private static OwnershipCheckResult ValidateWindows(string path, IAnalysisLogger? logger)
        {
#if CORECLR
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return new OwnershipCheckResult(true);
                }

                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    var dirInfo = new DirectoryInfo(path);
                    if (!dirInfo.Exists)
                    {
                        return new OwnershipCheckResult(true);
                    }
                }

                // On Windows we rely on the OS-level ACL enforcement.
                // A full ACL audit is deferred to a future iteration since it requires
                // referencing System.IO.FileSystem.AccessControl which may not be available
                // on all target frameworks. For now, the path validation + explicit opt-in
                // are the primary mitigations on Windows.
            }
            catch (Exception ex)
            {
                logger?.Warning($"Could not check ACL for '{path}': {ex.Message}");
            }
#endif

            return new OwnershipCheckResult(true);
        }

#if CORECLR
        private static bool IsOwnedByCurrentUserOrRoot(string path, IAnalysisLogger? logger)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                return true;
            }

            try
            {
                uint currentUid = UnixNativeMethods.GetUid();
                uint fileUid = UnixNativeMethods.GetFileOwnerUid(path);

                return fileUid == currentUid || fileUid == 0;
            }
            catch (Exception ex)
            {
                logger?.Warning($"Could not determine owner of '{path}': {ex.Message}. Allowing by default.");
                return true;
            }
        }
#endif

        private static StringComparison PathComparison =>
#if CORECLR
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
#else
            StringComparison.OrdinalIgnoreCase;
#endif
    }

#if CORECLR
    internal static class UnixNativeMethods
    {
        [DllImport("libc", EntryPoint = "getuid")]
        internal static extern uint GetUid();

        [DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUid();

        internal static uint GetFileOwnerUid(string path)
        {
            if (OperatingSystem.IsLinux())
            {
                return GetFileOwnerUidLinux(path);
            }

            if (OperatingSystem.IsMacOS())
            {
                return GetFileOwnerUidMacOS(path);
            }

            throw new PlatformNotSupportedException("File owner check is only supported on Linux and macOS.");
        }

        private static uint GetFileOwnerUidLinux(string path)
        {
            // Linux x64/ARM64: st_uid is at offset 24 in struct stat (4 bytes)
            const int StatBufSize = 256;
            const int UidOffset = 24;

            IntPtr buf = Marshal.AllocHGlobal(StatBufSize);
            try
            {
                int result = Lstat(path, buf);
                if (result != 0)
                {
                    throw new InvalidOperationException(
                        $"lstat failed for '{path}' with errno {Marshal.GetLastPInvokeError()}");
                }

                return (uint)Marshal.ReadInt32(buf, UidOffset);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        private static uint GetFileOwnerUidMacOS(string path)
        {
            // macOS ARM64/x64: st_uid is at offset 16 in struct stat (4 bytes)
            const int StatBufSize = 256;
            const int UidOffset = 16;

            IntPtr buf = Marshal.AllocHGlobal(StatBufSize);
            try
            {
                int result = MacOSLstat(path, buf);
                if (result != 0)
                {
                    throw new InvalidOperationException(
                        $"lstat failed for '{path}' with errno {Marshal.GetLastPInvokeError()}");
                }

                return (uint)Marshal.ReadInt32(buf, UidOffset);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
        private static extern int Lstat(
            [MarshalAs(UnmanagedType.LPStr)] string path,
            IntPtr buf);

        [DllImport("libc", EntryPoint = "lstat$INODE64", SetLastError = true)]
        private static extern int MacOSLstat(
            [MarshalAs(UnmanagedType.LPStr)] string path,
            IntPtr buf);
    }
#endif
}
