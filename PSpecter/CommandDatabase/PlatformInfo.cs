#nullable disable

using System;
using System.Collections.Generic;

namespace PSpecter.CommandDatabase
{
    public sealed class PlatformInfo : IEquatable<PlatformInfo>
    {
        public PlatformInfo(string edition, string version, string os)
        {
            Edition = edition ?? throw new ArgumentNullException(nameof(edition));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            OS = os ?? throw new ArgumentNullException(nameof(os));
        }

        /// <summary>Core or Desktop.</summary>
        public string Edition { get; }

        /// <summary>PowerShell version string, e.g. "7.4.7" or "5.1.17763.316".</summary>
        public string Version { get; }

        /// <summary>Operating system: "windows", "macos", or "linux".</summary>
        public string OS { get; }

        public bool Equals(PlatformInfo other)
        {
            if (other is null)
            {
                return false;
            }
            return string.Equals(Edition, other.Edition, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(OS, other.OS, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as PlatformInfo);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Edition);
                hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Version);
                hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(OS);
                return hash;
            }
        }

        public override string ToString() => $"{Edition}/{Version}/{OS}";
    }
}
