using System;
using System.Text.RegularExpressions;

namespace PSpecter.PssaCompatibility
{
    /// <summary>
    /// Maps rule names between the original PSSA format (e.g. "PSAvoidFoo")
    /// and the ScriptAnalyzer2 engine format (e.g. "PS/AvoidFoo").
    /// </summary>
    internal static class RuleNameMapper
    {
        /// <summary>
        /// Converts a PSSA-style rule name to the engine's full name format.
        /// "PSAvoidFoo" -> "PS/AvoidFoo"
        /// If the name already contains "/", it's assumed to be in engine format.
        /// </summary>
        public static string ToEngineFullName(string? pssaRuleName)
        {
            if (string.IsNullOrEmpty(pssaRuleName))
            {
                return pssaRuleName!;
            }

            if (pssaRuleName.Contains("/"))
            {
                return pssaRuleName;
            }

            if (pssaRuleName.StartsWith("PSDSC", StringComparison.OrdinalIgnoreCase)
                && pssaRuleName.Length > 5
                && char.IsUpper(pssaRuleName[5]))
            {
                return "PSDSC/" + pssaRuleName.Substring(5);
            }

            if (pssaRuleName.StartsWith("PS", StringComparison.OrdinalIgnoreCase)
                && pssaRuleName.Length > 2
                && char.IsUpper(pssaRuleName[2]))
            {
                return "PS/" + pssaRuleName.Substring(2);
            }

            return pssaRuleName;
        }

        /// <summary>
        /// Converts an engine full name to PSSA-style rule name.
        /// "PS/AvoidFoo" -> "PSAvoidFoo"
        /// </summary>
        public static string ToPssaRuleName(string? engineFullName)
        {
            if (string.IsNullOrEmpty(engineFullName))
            {
                return engineFullName!;
            }

            int slashIndex = engineFullName.IndexOf('/');
            if (slashIndex >= 0)
            {
                return engineFullName.Substring(0, slashIndex) + engineFullName.Substring(slashIndex + 1);
            }

            return engineFullName;
        }

        /// <summary>
        /// Converts an engine short name to PSSA-style rule name.
        /// "AvoidFoo" -> "PSAvoidFoo"
        /// </summary>
        public static string ToPssaName(string? engineShortName)
        {
            if (string.IsNullOrEmpty(engineShortName))
            {
                return engineShortName!;
            }

            if (engineShortName.StartsWith("PS", StringComparison.OrdinalIgnoreCase))
            {
                return engineShortName;
            }

            return "PS" + engineShortName;
        }

        /// <summary>
        /// Checks whether a PSSA-style rule name (potentially with wildcards) matches a given rule name.
        /// Supports '*' wildcards like the original PSSA.
        /// </summary>
        public static bool IsMatch(string pattern, string ruleName)
        {
            if (string.Equals(pattern, ruleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!pattern.Contains("*"))
            {
                return false;
            }

            string regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
            return Regex.IsMatch(ruleName, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
