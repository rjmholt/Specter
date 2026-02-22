using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;

namespace PSpecter.Tools
{
    /// <summary>
    /// Centralized well-known PowerShell metadata used by multiple analysis rules.
    /// </summary>
    public static class PowerShellConstants
    {
        /// <summary>
        /// The set of PowerShell approved verbs, collected via reflection from
        /// VerbsCommon, VerbsCommunications, VerbsData, VerbsDiagnostic,
        /// VerbsLifecycle, VerbsSecurity, and VerbsOther.
        /// </summary>
        public static HashSet<string> ApprovedVerbs { get; } = BuildApprovedVerbs();

        /// <summary>
        /// The common parameter names automatically added to functions with [CmdletBinding()].
        /// </summary>
        public static HashSet<string> CommonParameterNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Verbose",
            "Debug",
            "ErrorAction",
            "WarningAction",
            "InformationAction",
            "ErrorVariable",
            "WarningVariable",
            "InformationVariable",
            "OutVariable",
            "OutBuffer",
            "PipelineVariable",
        };

        private static HashSet<string> BuildApprovedVerbs()
        {
            var verbTypes = new[]
            {
                typeof(VerbsCommon),
                typeof(VerbsCommunications),
                typeof(VerbsData),
                typeof(VerbsDiagnostic),
                typeof(VerbsLifecycle),
                typeof(VerbsSecurity),
                typeof(VerbsOther),
            };

            var verbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Type verbType in verbTypes)
            {
                foreach (FieldInfo field in verbType.GetFields())
                {
                    if (field.IsLiteral)
                    {
                        verbs.Add(field.Name);
                    }
                }
            }

            return verbs;
        }
    }
}
