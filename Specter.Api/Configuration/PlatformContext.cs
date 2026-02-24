using Specter.CommandDatabase;
using System;
using System.Collections.Generic;

namespace Specter.Configuration
{
    public sealed class PlatformContext
    {
        public static PlatformContext Empty { get; } = new PlatformContext(Array.Empty<PlatformInfo>());

        private readonly IReadOnlyList<PlatformInfo> _targetPlatforms;

        public PlatformContext(IReadOnlyList<PlatformInfo>? targetPlatforms)
        {
            if (targetPlatforms is null || targetPlatforms.Count == 0)
            {
                _targetPlatforms = Array.Empty<PlatformInfo>();
                MinimumPSVersion = new Version(0, 0);
                return;
            }

            var distinctTargets = new List<PlatformInfo>(targetPlatforms.Count);
            var dedupe = new HashSet<PlatformInfo>();
            Version? minVersion = null;

            for (int i = 0; i < targetPlatforms.Count; i++)
            {
                PlatformInfo target = targetPlatforms[i];
                if (!dedupe.Add(target))
                {
                    continue;
                }

                distinctTargets.Add(target);
                if (minVersion is null || target.Version < minVersion)
                {
                    minVersion = target.Version;
                }
            }

            _targetPlatforms = distinctTargets;
            MinimumPSVersion = minVersion ?? new Version(0, 0);
        }

        public IReadOnlyList<PlatformInfo> TargetPlatforms => _targetPlatforms;

        public Version MinimumPSVersion { get; }

        public bool HasDesktopTarget => HasEdition("Desktop");

        public bool HasCoreTarget => HasEdition("Core");

        public bool AllTargetsAtLeast(Version version)
        {
            if (_targetPlatforms.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < _targetPlatforms.Count; i++)
            {
                if (_targetPlatforms[i].Version < version)
                {
                    return false;
                }
            }

            return true;
        }

        public bool AnyTargetBelow(Version version)
        {
            for (int i = 0; i < _targetPlatforms.Count; i++)
            {
                if (_targetPlatforms[i].Version < version)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasEdition(string edition)
        {
            for (int i = 0; i < _targetPlatforms.Count; i++)
            {
                if (string.Equals(_targetPlatforms[i].Edition, edition, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
