using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSpecter;
using PSpecter.Builder;
using PSpecter.Builtin;
using PSpecter.Configuration;
using PSpecter.Execution;
using PSpecter.Formatting;
using PSpecter.Rules;
using PSpecter.CommandDatabase;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;

#if !CORECLR
using PSpecter.Internal;
#endif

namespace PSpecter.PssaCompatibility.Commands
{
    [Cmdlet(VerbsLifecycle.Invoke, "ScriptAnalyzer", DefaultParameterSetName = "File")]
    [OutputType(typeof(DiagnosticRecord))]
    public class InvokeScriptAnalyzerCommand : PSCmdlet
    {
        private ScriptAnalyzer _scriptAnalyzer;
        private string[] _effectiveIncludeRules;
        private string[] _effectiveExcludeRules;
        private string[] _effectiveSeverity;

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "File",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        [Alias("PSPath")]
        public string Path { get; set; }

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "ScriptDefinition",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public string ScriptDefinition { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[] IncludeRule { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[] ExcludeRule { get; set; }

        [ValidateSet("Warning", "Error", "Information", "ParseError", IgnoreCase = true)]
        [Parameter]
        public string[] Severity { get; set; }

        [Parameter]
        [ValidateNotNull]
        public object Settings { get; set; }

        [Parameter]
        public SwitchParameter Recurse { get; set; }

        [Parameter]
        public SwitchParameter Fix { get; set; }

        [Parameter]
        public SwitchParameter EnableExit { get; set; }

        [Parameter]
        public SwitchParameter ReportSummary { get; set; }

        [Parameter]
        public SwitchParameter SuppressedOnly { get; set; }

        protected override void BeginProcessing()
        {
            _scriptAnalyzer = BuildAnalyzer();
        }

        protected override void ProcessRecord()
        {
            IReadOnlyCollection<ScriptDiagnostic> diagnostics;

            try
            {
                if (ParameterSetName == "File")
                {
                    string resolvedPath = ResolvePath(Path);
                    if (resolvedPath == null)
                    {
                        return;
                    }

                    diagnostics = _scriptAnalyzer.AnalyzeScriptPath(resolvedPath);
                }
                else
                {
                    diagnostics = _scriptAnalyzer.AnalyzeScriptInput(ScriptDefinition);
                }
            }
            catch (AggregateException ae)
            {
                foreach (Exception inner in ae.Flatten().InnerExceptions)
                {
                    WriteError(new ErrorRecord(
                        inner,
                        "RuleExecutionError",
                        ErrorCategory.InvalidOperation,
                        null));
                }
                return;
            }

            HashSet<string> severityFilter = BuildSeverityFilter();

            foreach (ScriptDiagnostic diagnostic in diagnostics)
            {
                DiagnosticRecord record = DiagnosticRecord.FromEngineDiagnostic(diagnostic);

                if (!PassesIncludeFilter(record.RuleName))
                {
                    continue;
                }

                if (IsExcluded(record.RuleName))
                {
                    continue;
                }

                if (severityFilter != null && !severityFilter.Contains(record.Severity.ToString()))
                {
                    continue;
                }

                WriteObject(record);
            }
        }

        private string ResolvePath(string inputPath)
        {
            try
            {
                var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(inputPath);
                if (resolved.Count > 0)
                {
                    return resolved[0].Path;
                }
            }
            catch (ItemNotFoundException)
            {
                WriteError(new ErrorRecord(
                    new FileNotFoundException($"Cannot find path '{inputPath}' because it does not exist.", inputPath),
                    "FileNotFound",
                    ErrorCategory.ObjectNotFound,
                    inputPath));
            }

            return null;
        }

        private bool PassesIncludeFilter(string ruleName)
        {
            if (_effectiveIncludeRules is null || _effectiveIncludeRules.Length == 0)
            {
                return true;
            }

            foreach (string pattern in _effectiveIncludeRules)
            {
                if (RuleNameMapper.IsMatch(pattern, ruleName))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsExcluded(string ruleName)
        {
            if (_effectiveExcludeRules is null || _effectiveExcludeRules.Length == 0)
            {
                return false;
            }

            foreach (string pattern in _effectiveExcludeRules)
            {
                if (RuleNameMapper.IsMatch(pattern, ruleName))
                {
                    return true;
                }
            }

            return false;
        }

        private HashSet<string> BuildSeverityFilter()
        {
            if (_effectiveSeverity is null || _effectiveSeverity.Length == 0)
            {
                return null;
            }

            return new HashSet<string>(_effectiveSeverity, StringComparer.OrdinalIgnoreCase);
        }

        private Settings ResolveSettings()
        {
            object settingsInput = Settings;

            if (settingsInput is PSObject psObj)
            {
                settingsInput = psObj.BaseObject;
            }

            if (settingsInput == null)
            {
                // Auto-discover PSScriptAnalyzerSettings.psd1 from target path or CWD
                string searchPath = ParameterSetName == "File" ? Path : SessionState.Path.CurrentFileSystemLocation.Path;
                if (searchPath != null)
                {
                    var directory = searchPath.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                    if (File.Exists(directory))
                    {
                        directory = System.IO.Path.GetDirectoryName(directory);
                    }

                    if (Directory.Exists(directory))
                    {
                        var autoSettingsPath = System.IO.Path.Combine(directory, "PSScriptAnalyzerSettings.psd1");
                        if (File.Exists(autoSettingsPath))
                        {
                            WriteVerbose($"Auto-discovered settings file: {autoSettingsPath}");
                            settingsInput = autoSettingsPath;
                        }
                    }
                }
            }

            if (settingsInput == null)
            {
                return null;
            }

            // Resolve relative file paths
            if (settingsInput is string settingsPath)
            {
                try
                {
                    var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(settingsPath);
                    if (resolved.Count > 0)
                    {
                        settingsInput = resolved[0].Path;
                    }
                }
                catch
                {
                    // If resolution fails, keep the original path and let Settings handle the error
                }
            }

            try
            {
                return new Settings(settingsInput);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                    ex,
                    "InvalidSettings",
                    ErrorCategory.InvalidArgument,
                    settingsInput));
                return null;
            }
        }

        private ScriptAnalyzer BuildAnalyzer()
        {
            IPowerShellCommandDatabase commandDb = SessionStateCommandDatabase.Create(
                SessionState.InvokeCommand);

            // Start with default rule configurations
            var configDict = new Dictionary<string, IRuleConfiguration>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in Default.RuleConfiguration)
            {
                configDict[kvp.Key] = kvp.Value;
            }

            // Parse settings and merge
            Settings parsedSettings = ResolveSettings();
            if (parsedSettings != null)
            {
                // Settings values serve as defaults; explicit cmdlet parameters override
                _effectiveIncludeRules = IncludeRule ?? parsedSettings.IncludeRules?.ToArray();
                _effectiveExcludeRules = ExcludeRule ?? parsedSettings.ExcludeRules?.ToArray();
                _effectiveSeverity = Severity ?? parsedSettings.Severities?.ToArray();

                // Apply rule arguments from settings to configuration objects
                if (parsedSettings.RuleArguments != null && parsedSettings.RuleArguments.Count > 0)
                {
                    ApplyRuleArguments(configDict, parsedSettings.RuleArguments);
                }
            }
            else
            {
                _effectiveIncludeRules = IncludeRule;
                _effectiveExcludeRules = ExcludeRule;
                _effectiveSeverity = Severity;
            }

            return new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(rcpb => rcpb.AddSingleton(commandDb))
                .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
                .AddBuiltinRules(configDict)
                .Build();
        }

        private static void ApplyRuleArguments(
            Dictionary<string, IRuleConfiguration> configDict,
            Dictionary<string, Dictionary<string, object>> ruleArguments)
        {
            // Build a mapping from PSSA rule name -> (engine config key, config type)
            var ruleConfigMap = BuildRuleConfigMap();

            foreach (var ruleEntry in ruleArguments)
            {
                string pssaRuleName = ruleEntry.Key;
                Dictionary<string, object> args = ruleEntry.Value;

                if (!ruleConfigMap.TryGetValue(pssaRuleName, out var ruleMapping))
                {
                    continue;
                }

                IRuleConfiguration config = CreateConfigFromArguments(ruleMapping.ConfigType, args);
                if (config != null)
                {
                    configDict[ruleMapping.EngineConfigKey] = config;
                }
            }
        }

        private static IRuleConfiguration CreateConfigFromArguments(Type configType, Dictionary<string, object> args)
        {
            try
            {
                object config = Activator.CreateInstance(configType);

                foreach (var arg in args)
                {
                    // PSSA uses "Enable" to toggle rules; map it to Common.Enabled for editor configs
                    if (string.Equals(arg.Key, "Enable", StringComparison.OrdinalIgnoreCase)
                        && config is IEditorConfiguration editorConfig)
                    {
                        editorConfig.Common.Enabled = Convert.ToBoolean(arg.Value);
                        continue;
                    }

                    var prop = configType.GetProperty(
                        arg.Key,
                        System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.IgnoreCase);

                    if (prop == null || !prop.CanWrite)
                    {
                        continue;
                    }

                    object convertedValue = ConvertSettingsValue(arg.Value, prop.PropertyType);
                    if (convertedValue != null || !prop.PropertyType.IsValueType)
                    {
                        prop.SetValue(config, convertedValue);
                    }
                }

                return (IRuleConfiguration)config;
            }
            catch
            {
                return null;
            }
        }

        private static object ConvertSettingsValue(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // object[] -> IReadOnlyCollection<string>, string[], List<string>, etc.
            if (value is object[] objArray)
            {
                if (targetType == typeof(IReadOnlyCollection<string>)
                    || targetType == typeof(IEnumerable<string>)
                    || targetType == typeof(IReadOnlyList<string>)
                    || targetType == typeof(string[]))
                {
                    return objArray.Select(o => o?.ToString()).ToArray();
                }

                if (targetType == typeof(List<string>))
                {
                    return objArray.Select(o => o?.ToString()).ToList();
                }
            }

            // string[] -> IReadOnlyCollection<string>
            if (value is string[] strArray)
            {
                if (typeof(IEnumerable<string>).IsAssignableFrom(targetType))
                {
                    return strArray;
                }
            }

            // Scalar conversions
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        private struct RuleConfigMapping
        {
            public string EngineConfigKey;
            public Type ConfigType;
        }

        private static Dictionary<string, RuleConfigMapping> BuildRuleConfigMap()
        {
            var map = new Dictionary<string, RuleConfigMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (Type ruleType in BuiltinRules.DefaultRules)
            {
                if (!RuleInfo.TryGetFromRuleType(ruleType, out RuleInfo ruleInfo))
                {
                    continue;
                }

                Type configType = FindConfigurationType(ruleType);
                if (configType == null)
                {
                    continue;
                }

                string pssaName = RuleNameMapper.ToPssaName(ruleInfo.Name);
                map[pssaName] = new RuleConfigMapping
                {
                    EngineConfigKey = ruleInfo.FullName,
                    ConfigType = configType,
                };
            }

            return map;
        }

        private static Type FindConfigurationType(Type ruleType)
        {
            foreach (Type iface in ruleType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IConfigurableRule<>))
                {
                    return iface.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
