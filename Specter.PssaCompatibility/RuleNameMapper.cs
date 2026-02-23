using System;
using System.Text.RegularExpressions;

namespace Specter.PssaCompatibility
{
    /// <summary>
    /// Maps rule names between the original PSSA format (e.g. "PSAvoidFoo")
    /// and the Specter engine format (e.g. "PS/AvoidFoo").
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

            string name = pssaRuleName!;

            if (name.Contains("/"))
            {
                return name;
            }

            if (name.StartsWith("PSDSC", StringComparison.OrdinalIgnoreCase)
                && name.Length > 5
                && char.IsUpper(name[5]))
            {
                return "PSDSC/" + name.Substring(5);
            }

            if (name.StartsWith("PS", StringComparison.OrdinalIgnoreCase)
                && name.Length > 2
                && char.IsUpper(name[2]))
            {
                return "PS/" + name.Substring(2);
            }

            return name;
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

            string name = engineFullName!;
            int slashIndex = name.IndexOf('/');
            if (slashIndex >= 0)
            {
                return name.Substring(0, slashIndex) + name.Substring(slashIndex + 1);
            }

            return name;
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

            string name = engineShortName!;

            if (name.StartsWith("PS", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return "PS" + name;
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
