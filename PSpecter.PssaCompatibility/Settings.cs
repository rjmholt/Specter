#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer
{
    internal enum SettingsMode { None = 0, Auto, File, Hashtable, Preset }

    /// <summary>
    /// Compatibility implementation of the original PSScriptAnalyzer Settings class.
    /// Accepts a hashtable or a path to a .psd1 settings file.
    /// </summary>
    public class Settings
    {
        public bool RecurseCustomRulePath { get; private set; }
        public bool IncludeDefaultRules { get; private set; }
        public string FilePath { get; private set; }
        public IEnumerable<string> IncludeRules { get; private set; }
        public IEnumerable<string> ExcludeRules { get; private set; }
        public IEnumerable<string> Severities { get; private set; }
        public IEnumerable<string> CustomRulePath { get; private set; }
        public Dictionary<string, Dictionary<string, object>> RuleArguments { get; private set; }

        public Settings(object settings) : this(settings, null)
        {
        }

        public Settings(object settings, Func<string, string> presetResolver)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            IncludeRules = new List<string>();
            ExcludeRules = new List<string>();
            Severities = new List<string>();
            CustomRulePath = new List<string>();
            RuleArguments = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

            if (settings is PSObject psObj)
            {
                settings = psObj.BaseObject;
            }

            if (settings is string settingsFilePath)
            {
                if (presetResolver != null)
                {
                    var resolvedFilePath = presetResolver(settingsFilePath);
                    if (resolvedFilePath != null)
                    {
                        settingsFilePath = resolvedFilePath;
                    }
                }

                if (File.Exists(settingsFilePath))
                {
                    FilePath = settingsFilePath;
                    ParseSettingsFile(settingsFilePath);
                }
                else
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "Invalid path '{0}'.", settingsFilePath));
                }
            }
            else if (settings is Hashtable settingsHashtable)
            {
                ParseSettingsHashtable(settingsHashtable);
            }
            else
            {
                throw new ArgumentException("Settings must be a hashtable or a path to a .psd1 file.");
            }
        }

        internal static SettingsMode FindSettingsMode(object settings, string path, out object settingsFound)
        {
            var settingsMode = SettingsMode.None;

            if (settings is PSObject settingsFoundPSObject)
            {
                settings = settingsFoundPSObject.BaseObject;
            }

            settingsFound = settings;
            if (settingsFound == null)
            {
                if (path != null)
                {
                    var directory = path.TrimEnd(Path.DirectorySeparatorChar);
                    if (File.Exists(directory))
                    {
                        directory = Path.GetDirectoryName(directory);
                    }

                    if (Directory.Exists(directory))
                    {
                        var settingsFilePath = Path.Combine(directory, "PSScriptAnalyzerSettings.psd1");
                        settingsFound = settingsFilePath;
                        if (File.Exists(settingsFilePath))
                        {
                            settingsMode = SettingsMode.Auto;
                        }
                    }
                }
            }
            else
            {
                if (settingsFound is string settingsString)
                {
                    settingsMode = SettingsMode.File;
                }
                else if (settingsFound is Hashtable)
                {
                    settingsMode = SettingsMode.Hashtable;
                }
            }

            return settingsMode;
        }

        private Dictionary<string, object> GetDictionaryFromHashtable(Hashtable hashtable)
        {
            var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in hashtable.Keys)
            {
                string key = obj as string;
                if (key == null)
                {
                    throw new InvalidDataException(
                        string.Format(CultureInfo.CurrentCulture, "Key '{0}' is not a string.", obj));
                }

                var valueObj = hashtable[obj];
                if (valueObj is Hashtable valueHashtable)
                {
                    dictionary.Add(key, GetDictionaryFromHashtable(valueHashtable));
                }
                else
                {
                    dictionary.Add(key, valueObj);
                }
            }
            return dictionary;
        }

        private List<string> GetData(object val, string key)
        {
            if (val == null)
            {
                throw new InvalidDataException(
                    string.Format(CultureInfo.CurrentCulture, "Wrong value for key '{0}'.", key));
            }

            var values = new List<string>();
            if (val is string valueStr)
            {
                values.Add(valueStr);
            }
            else
            {
                var valueArr = val as object[] ?? val as string[];
                if (valueArr != null)
                {
                    foreach (var item in valueArr)
                    {
                        if (item is string itemStr)
                        {
                            values.Add(itemStr);
                        }
                        else
                        {
                            throw new InvalidDataException(
                                string.Format(CultureInfo.CurrentCulture, "Wrong value '{0}' for key '{1}'.", val, key));
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException(
                        string.Format(CultureInfo.CurrentCulture, "Wrong value '{0}' for key '{1}'.", val, key));
                }
            }

            return values;
        }

        private Dictionary<string, Dictionary<string, object>> ConvertToRuleArgumentType(object ruleArguments)
        {
            var ruleArgs = ruleArguments as Dictionary<string, object>;
            if (ruleArgs == null)
            {
                throw new ArgumentException("Rules value must be a dictionary.", nameof(ruleArguments));
            }

            var ruleArgsDict = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in ruleArgs.Keys)
            {
                var argsDict = ruleArgs[rule] as Dictionary<string, object>;
                if (argsDict == null)
                {
                    throw new InvalidDataException("Each rule's arguments must be a dictionary.");
                }
                ruleArgsDict[rule] = argsDict;
            }

            return ruleArgsDict;
        }

        private void ParseSettingsHashtable(Hashtable settingsHashtable)
        {
            var settings = GetDictionaryFromHashtable(settingsHashtable);
            foreach (var settingKey in settings.Keys)
            {
                var key = settingKey.ToLowerInvariant();
                object val = settings[settingKey];
                switch (key)
                {
                    case "severity":
                        Severities = GetData(val, key);
                        break;

                    case "includerules":
                        IncludeRules = GetData(val, key);
                        break;

                    case "excluderules":
                        ExcludeRules = GetData(val, key);
                        break;

                    case "customrulepath":
                        CustomRulePath = GetData(val, key);
                        break;

                    case "includedefaultrules":
                        if (!(val is bool boolVal1))
                        {
                            throw new InvalidDataException(string.Format(
                                CultureInfo.CurrentCulture,
                                "Value for key '{0}' must be a boolean.",
                                settingKey));
                        }
                        IncludeDefaultRules = boolVal1;
                        break;

                    case "recursecustomrulepath":
                        if (!(val is bool boolVal2))
                        {
                            throw new InvalidDataException(string.Format(
                                CultureInfo.CurrentCulture,
                                "Value for key '{0}' must be a boolean.",
                                settingKey));
                        }
                        RecurseCustomRulePath = boolVal2;
                        break;

                    case "rules":
                        try
                        {
                            RuleArguments = ConvertToRuleArgumentType(val);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new InvalidDataException(
                                string.Format(CultureInfo.CurrentCulture, "Wrong value for key '{0}'.", key),
                                ex);
                        }
                        break;

                    default:
                        throw new InvalidDataException(
                            string.Format(CultureInfo.CurrentCulture, "Unknown key '{0}' in settings.", key));
                }
            }
        }

        private void ParseSettingsFile(string settingsFilePath)
        {
            Token[] parserTokens = null;
            ParseError[] parserErrors = null;
            Ast profileAst = Parser.ParseFile(settingsFilePath, out parserTokens, out parserErrors);
            IEnumerable<Ast> hashTableAsts = profileAst.FindAll(item => item is HashtableAst, false);

            if (!hashTableAsts.Any())
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Invalid settings file: {0}", settingsFilePath));
            }

            HashtableAst hashTableAst = hashTableAsts.First() as HashtableAst;
            Hashtable hashtable;
            try
            {
                hashtable = Helper.GetSafeValueFromHashtableAst(hashTableAst);
            }
            catch (InvalidOperationException e)
            {
                throw new ArgumentException("Invalid settings file.", e);
            }

            if (hashtable == null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Invalid settings file: {0}", settingsFilePath));
            }

            ParseSettingsHashtable(hashtable);
        }
    }
}
