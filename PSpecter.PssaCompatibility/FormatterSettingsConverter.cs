using System;
using System.Collections.Generic;
using System.Reflection;
using PSpecter.Builtin;
using PSpecter.Builtin.Editors;
using PSpecter.Formatting;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;

namespace PSpecter.PssaCompatibility
{
    /// <summary>
    /// Translates PSSA-style settings (preset names, hashtable rule arguments)
    /// into a config dictionary that <see cref="ScriptFormatter.FromEditorConfigs"/> can consume.
    /// Uses <see cref="EditorNameMapper"/> for all PSSA-to-engine name translations.
    /// </summary>
    internal static class FormatterSettingsConverter
    {
        private static readonly Dictionary<string, IReadOnlyDictionary<string, IEditorConfiguration>> s_presets =
            new Dictionary<string, IReadOnlyDictionary<string, IEditorConfiguration>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CodeFormatting"] = FormatterPresets.Default,
                ["OTBS"] = FormatterPresets.OTBS,
                ["CodeFormattingOTBS"] = FormatterPresets.OTBS,
                ["Allman"] = FormatterPresets.Allman,
                ["CodeFormattingAllman"] = FormatterPresets.Allman,
                ["Stroustrup"] = FormatterPresets.Stroustrup,
                ["CodeFormattingStroustrup"] = FormatterPresets.Stroustrup,
            };

        /// <summary>
        /// Cache mapping engine editor names to their <see cref="IEditorConfiguration"/> types.
        /// Populated on first use from <see cref="BuiltinEditors.DefaultEditors"/>.
        /// </summary>
        private static readonly Dictionary<string, Type> s_editorConfigTypes = BuildEditorConfigTypeMap();

        public static bool TryGetPreset(string name, out IReadOnlyDictionary<string, IEditorConfiguration> configs)
        {
            return s_presets.TryGetValue(name, out configs);
        }

        /// <summary>
        /// Converts PSSA settings to a config dictionary.
        /// Only editors whose PSSA rules appear in <see cref="Settings.RuleArguments"/> are enabled;
        /// all others start disabled.
        /// </summary>
        public static IReadOnlyDictionary<string, IEditorConfiguration> FromSettings(Settings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));

            var configs = new Dictionary<string, IEditorConfiguration>(StringComparer.OrdinalIgnoreCase);

            if (settings.RuleArguments == null || settings.RuleArguments.Count == 0)
            {
                return configs;
            }

            foreach (var mapping in EditorNameMapper.GetAllMappings())
            {
                string pssaName = mapping.Key;
                string editorName = mapping.Value;

                if (!settings.RuleArguments.TryGetValue(pssaName, out var pssaArgs))
                {
                    continue;
                }

                if (!s_editorConfigTypes.TryGetValue(editorName, out Type configType))
                {
                    continue;
                }

                IEditorConfiguration editorConfig = CreateAndPopulateConfig(configType, pssaName, pssaArgs);
                if (editorConfig != null)
                {
                    configs[editorName] = editorConfig;
                }
            }

            return configs;
        }

        private static IEditorConfiguration CreateAndPopulateConfig(
            Type configType, string pssaRuleName, Dictionary<string, object> pssaArgs)
        {
            var config = (IEditorConfiguration)Activator.CreateInstance(configType);

            bool enabled = GetBool(pssaArgs, "Enable", true);
            config.Common.Enabled = enabled;

            foreach (var kvp in pssaArgs)
            {
                if (string.Equals(kvp.Key, "Enable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Handle PSSA "Kind" -> engine "UseTabs" special case
                if (string.Equals(pssaRuleName, "PSUseConsistentIndentation", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(kvp.Key, "Kind", StringComparison.OrdinalIgnoreCase))
                {
                    PropertyInfo useTabsProp = configType.GetProperty("UseTabs");
                    if (useTabsProp != null)
                    {
                        bool useTabs = string.Equals(kvp.Value?.ToString(), "tab", StringComparison.OrdinalIgnoreCase);
                        useTabsProp.SetValue(config, useTabs);
                    }
                    continue;
                }

                // Map property name: try explicit mapping, then fall back to same name
                EditorNameMapper.TryGetPropertyName(pssaRuleName, kvp.Key, out string enginePropName);

                PropertyInfo prop = configType.GetProperty(enginePropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite)
                {
                    continue;
                }

                object value = ConvertValue(kvp.Value, prop.PropertyType);
                if (value != null)
                {
                    prop.SetValue(config, value);
                }
            }

            return config;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                if (value is string s && bool.TryParse(s, out bool parsed)) return parsed;
                return null;
            }

            if (targetType == typeof(int))
            {
                if (value is int i) return i;
                if (int.TryParse(value.ToString(), out int parsed)) return parsed;
                return null;
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType.IsEnum)
            {
                string strVal = value.ToString();
                try
                {
                    return Enum.Parse(targetType, strVal, ignoreCase: true);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (!TryGetValue(args, key, out object val))
            {
                return defaultValue;
            }

            if (val is bool b) return b;
            if (val is string s && bool.TryParse(s, out bool parsed)) return parsed;

            return defaultValue;
        }

        private static bool TryGetValue(Dictionary<string, object> args, string key, out object value)
        {
            foreach (var kvp in args)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static Dictionary<string, Type> BuildEditorConfigTypeMap()
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (Type editorType in BuiltinEditors.DefaultEditors)
            {
                if (!EditorInfo.TryGetFromEditorType(editorType, out EditorInfo info))
                {
                    continue;
                }

                Type configType = EditorInfo.GetConfigurationType(editorType);
                if (configType != null)
                {
                    map[info.Name] = configType;
                }
            }
            return map;
        }
    }
}
