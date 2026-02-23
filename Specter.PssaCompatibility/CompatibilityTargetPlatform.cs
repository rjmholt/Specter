using System;
using Specter.CommandDatabase;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic
{
    /// <summary>
    /// Exposes target platform data in the shape expected by PSSA compatibility tests:
    /// <c>TargetPlatform.OperatingSystem.Family</c> and <c>TargetPlatform.PowerShell.Version</c>.
    /// </summary>
    public sealed class CompatibilityTargetPlatform
    {
        public CompatibilityPowerShellInfo PowerShell { get; }
        public CompatibilityOperatingSystemInfo OperatingSystem { get; }

        private CompatibilityTargetPlatform(CompatibilityPowerShellInfo ps, CompatibilityOperatingSystemInfo os)
        {
            PowerShell = ps;
            OperatingSystem = os;
        }

        internal static CompatibilityTargetPlatform FromPlatformInfo(PlatformInfo pi)
        {
            return new CompatibilityTargetPlatform(
                new CompatibilityPowerShellInfo(pi.Edition, pi.Version),
                new CompatibilityOperatingSystemInfo(pi.Os.Family));
        }
    }

    public sealed class CompatibilityPowerShellInfo
    {
        public string Edition { get; }
        public Version Version { get; }

        internal CompatibilityPowerShellInfo(string edition, Version version)
        {
            Edition = edition;
            Version = version;
        }
    }

    public sealed class CompatibilityOperatingSystemInfo
    {
        public string Family { get; }

        internal CompatibilityOperatingSystemInfo(string family)
        {
            Family = family;
        }
    }
}
