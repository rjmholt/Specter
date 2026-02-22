using System;
using System.Collections.Generic;

namespace PSpecter.PssaCompatibility
{
    /// <summary>
    /// Maps PSSA rule names to PSLint editor names, and PSSA config property names
    /// to PSLint editor config property names. All PSSA-specific knowledge lives here.
    /// </summary>
    internal static class EditorNameMapper
    {
        /// <summary>
        /// Maps PSSA rule names (e.g. "PSPlaceOpenBrace") to engine editor names (e.g. "PlaceOpenBrace").
        /// </summary>
        private static readonly Dictionary<string, string> s_pssaToEditorName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PSPlaceOpenBrace"] = "PlaceOpenBrace",
                ["PSPlaceCloseBrace"] = "PlaceCloseBrace",
                ["PSUseConsistentWhitespace"] = "UseConsistentWhitespace",
                ["PSUseConsistentIndentation"] = "UseConsistentIndentation",
                ["PSAlignAssignmentStatement"] = "AlignAssignmentStatement",
                ["PSUseCorrectCasing"] = "UseCorrectCasing",
                ["PSAvoidTrailingWhitespace"] = "AvoidTrailingWhitespace",
            };

        /// <summary>
        /// Maps PSSA config property names to engine editor config property names.
        /// Only entries that differ need to be listed; identical names are handled by convention.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> s_propertyMappings =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PSUseConsistentIndentation"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // PSSA uses "Kind" with values "space"/"tab"; engine uses bool "UseTabs"
                    // This is handled as a special case in FormatterSettingsConverter, not a simple rename
                },
            };

        public static bool TryGetEditorName(string pssaRuleName, out string editorName)
        {
            return s_pssaToEditorName.TryGetValue(pssaRuleName, out editorName);
        }

        public static IEnumerable<KeyValuePair<string, string>> GetAllMappings()
        {
            return s_pssaToEditorName;
        }

        public static bool TryGetPropertyName(string pssaRuleName, string pssaPropertyName, out string editorPropertyName)
        {
            if (s_propertyMappings.TryGetValue(pssaRuleName, out var propMap)
                && propMap.TryGetValue(pssaPropertyName, out editorPropertyName))
            {
                return true;
            }

            editorPropertyName = pssaPropertyName;
            return false;
        }
    }
}
