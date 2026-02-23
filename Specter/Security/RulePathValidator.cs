using System;
using System.IO;
using Specter.Logging;

namespace Specter.Security
{
    internal enum RuleFileKind
    {
        Unknown,
        Assembly,
        ScriptModule,
        ModuleManifest,
        Directory,
    }

    internal readonly struct RulePathValidationResult
    {
        internal RulePathValidationResult(string canonicalPath, RuleFileKind kind)
        {
            CanonicalPath = canonicalPath;
            Kind = kind;
            IsValid = true;
            RejectionReason = null;
        }

        internal RulePathValidationResult(string rejectionReason)
        {
            CanonicalPath = null;
            Kind = RuleFileKind.Unknown;
            IsValid = false;
            RejectionReason = rejectionReason;
        }

        internal string? CanonicalPath { get; }

        internal RuleFileKind Kind { get; }

        internal bool IsValid { get; }

        internal string? RejectionReason { get; }
    }

    internal static class RulePathValidator
    {
        internal static RulePathValidationResult ValidatePath(
            string rawPath,
            string? allowedRoot,
            IAnalysisLogger? logger)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return Reject("Rule path is null or empty.", logger);
            }

            if (rawPath.IndexOf('\0') >= 0)
            {
                return Reject("Rule path contains null bytes.", logger);
            }

            string canonicalPath;
            try
            {
                canonicalPath = Path.GetFullPath(rawPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Reject($"Rule path '{rawPath}' is not a valid path: {ex.Message}", logger);
            }

            canonicalPath = ResolveSymlinks(canonicalPath);

            if (allowedRoot is not null)
            {
                string normalizedRoot = NormalizeDirPath(Path.GetFullPath(allowedRoot));
                string normalizedCanonical = NormalizeDirPath(canonicalPath);

                if (!normalizedCanonical.StartsWith(normalizedRoot, PathComparison))
                {
                    return Reject(
                        $"Rule path '{canonicalPath}' is outside the allowed root '{allowedRoot}'.",
                        logger);
                }
            }

            if (Directory.Exists(canonicalPath))
            {
                return new RulePathValidationResult(canonicalPath, RuleFileKind.Directory);
            }

            if (!File.Exists(canonicalPath))
            {
                return Reject($"Rule path '{canonicalPath}' does not exist.", logger);
            }

            RuleFileKind kind = ClassifyByExtension(canonicalPath);
            if (kind == RuleFileKind.Unknown)
            {
                return Reject(
                    $"Rule path '{canonicalPath}' has an unsupported extension. Expected .dll, .psm1, or .psd1.",
                    logger);
            }

            return new RulePathValidationResult(canonicalPath, kind);
        }

        internal static RuleFileKind ClassifyByExtension(string path)
        {
            string extension = Path.GetExtension(path);

            if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return RuleFileKind.Assembly;
            }

            if (extension.Equals(".psm1", StringComparison.OrdinalIgnoreCase))
            {
                return RuleFileKind.ScriptModule;
            }

            if (extension.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                return RuleFileKind.ModuleManifest;
            }

            return RuleFileKind.Unknown;
        }

        private static string ResolveSymlinks(string path)
        {
            try
            {
#if CORECLR
                var fileInfo = new FileInfo(path);
                string? resolved = fileInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
                return resolved ?? path;
#else
                // .NET Framework does not have ResolveLinkTarget; symlinks are rare on Windows
                return path;
#endif
            }
            catch
            {
                return path;
            }
        }

        private static string NormalizeDirPath(string path)
        {
            if (path.Length > 0
                && path[path.Length - 1] != Path.DirectorySeparatorChar
                && path[path.Length - 1] != Path.AltDirectorySeparatorChar)
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }

        private static RulePathValidationResult Reject(string reason, IAnalysisLogger? logger)
        {
            logger?.Warning(reason);
            return new RulePathValidationResult(reason);
        }

        private static StringComparison PathComparison =>
#if CORECLR
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
#else
            StringComparison.OrdinalIgnoreCase;
#endif
    }
}
