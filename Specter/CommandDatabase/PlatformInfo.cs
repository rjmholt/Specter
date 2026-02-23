using System;

namespace Specter.CommandDatabase
{
    /// <summary>
    /// Operating system metadata for a platform profile.
    /// </summary>
    public sealed class OsInfo : IEquatable<OsInfo>
    {
        public OsInfo(string family, string? version = null, int? skuId = null, string? architecture = null, string? environment = null)
        {
            Family = family ?? throw new ArgumentNullException(nameof(family));
            Version = version;
            SkuId = skuId;
            Architecture = architecture;
            Environment = environment;
        }

        /// <summary>"Windows", "Linux", or "MacOS".</summary>
        public string Family { get; }

        /// <summary>OS build string, e.g. "10.0.17763.0" on Windows, "18.04" on Ubuntu. Null when unknown.</summary>
        public string? Version { get; }

        /// <summary>Windows SKU ID (8=Server Datacenter, 48=Pro, 4=Enterprise). Null for non-Windows.</summary>
        public int? SkuId { get; }

        /// <summary>"X64", "Arm64", etc. Null when unknown.</summary>
        public string? Architecture { get; }

        /// <summary>
        /// Hosting environment for restricted/sandboxed platforms, e.g. "AzureFunctions".
        /// Null for standard OS installs.
        /// </summary>
        public string? Environment { get; }

        public bool Equals(OsInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Family, other.Family, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase)
                && SkuId == other.SkuId
                && string.Equals(Environment, other.Environment, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) => Equals(obj as OsInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Family);
                hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Version ?? string.Empty);
                hash = hash * 397 ^ (SkuId ?? 0);
                hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Environment ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            string result = Family;
            if (Version is not null)
            {
                result += $"({Version}";
                if (SkuId is not null)
                {
                    result += $",SKU={SkuId}";
                }
                if (Environment is not null)
                {
                    result += $",{Environment}";
                }
                result += ")";
            }
            else if (Environment is not null)
            {
                result += $"({Environment})";
            }
            return result;
        }
    }

    /// <summary>
    /// Identifies a PowerShell platform: edition, version, and OS metadata.
    /// </summary>
    public sealed class PlatformInfo : IEquatable<PlatformInfo>
    {
        public PlatformInfo(string edition, Version version, OsInfo os)
        {
            Edition = edition ?? throw new ArgumentNullException(nameof(edition));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Os = os ?? throw new ArgumentNullException(nameof(os));
        }

        /// <summary>
        /// Creates a <see cref="PlatformInfo"/> from a version string, stripping
        /// any pre-release label (e.g. "-rc1") before parsing.
        /// </summary>
        public static PlatformInfo Create(string edition, string versionStr, OsInfo os)
        {
            return new PlatformInfo(edition, ParseVersion(versionStr), os);
        }

        /// <summary>Core or Desktop.</summary>
        public string Edition { get; }

        /// <summary>Structured PowerShell version.</summary>
        public Version Version { get; }

        /// <summary>Operating system metadata.</summary>
        public OsInfo Os { get; }

        public bool Equals(PlatformInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Edition, other.Edition, StringComparison.OrdinalIgnoreCase)
                && Version.Equals(other.Version)
                && Os.Equals(other.Os);
        }

        public override bool Equals(object? obj) => Equals(obj as PlatformInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Edition);
                hash = hash * 397 ^ Version.GetHashCode();
                hash = hash * 397 ^ Os.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"{Edition}/{Version}/{Os}";

        internal static Version ParseVersion(string versionStr)
        {
            if (string.IsNullOrEmpty(versionStr))
            {
                return new Version(0, 0);
            }

            int dashIndex = versionStr.IndexOf('-');
            if (dashIndex >= 0)
            {
                versionStr = versionStr.Substring(0, dashIndex);
            }

            if (Version.TryParse(versionStr, out Version? v))
            {
                return v;
            }

            return new Version(0, 0);
        }
    }
}
