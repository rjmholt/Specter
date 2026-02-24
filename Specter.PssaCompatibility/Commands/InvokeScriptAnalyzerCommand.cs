using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Specter;
using Specter.Builder;
using Specter.Configuration;
using Specter.Execution;
using Specter.Formatting;
using Specter.Logging;
using Specter.Rules.Builtin;
using Specter.Rules;
using Specter.Security;
using Specter.Suppression;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;
using Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic;

using Specter.CommandDatabase;

#if !CORECLR
using Specter.Internal;
#endif

namespace Specter.PssaCompatibility.Commands
{
    [Cmdlet(VerbsLifecycle.Invoke, "ScriptAnalyzer", DefaultParameterSetName = "File")]
    [OutputType(typeof(DiagnosticRecord))]
    public class InvokeScriptAnalyzerCommand : PSCmdlet
    {
        private ScriptAnalyzer? _scriptAnalyzer;
        private string[]? _effectiveIncludeRules;
        private string[]? _effectiveExcludeRules;
        private string[]? _effectiveSeverity;

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "File",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        [Alias("PSPath")]
        public string Path { get; set; } = string.Empty;

        [Parameter(
            Position = 0,
            Mandatory = true,
            ParameterSetName = "ScriptDefinition",
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public string ScriptDefinition { get; set; } = string.Empty;

        [Parameter]
        [ValidateNotNull]
        public string[]? IncludeRule { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[]? ExcludeRule { get; set; }

        [ValidateSet("Warning", "Error", "Information", "ParseError", IgnoreCase = true)]
        [Parameter]
        public string[]? Severity { get; set; }

        [Parameter]
        [ValidateNotNull]
        public object? Settings { get; set; }

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

        [Parameter]
        public SwitchParameter IncludeSuppressed { get; set; }

        [Parameter]
        [ValidateNotNull]
        public string[]? CustomRulePath { get; set; }

        [Parameter]
        public SwitchParameter RecurseCustomRulePath { get; set; }

        protected override void BeginProcessing()
        {
            _scriptAnalyzer = BuildAnalyzer();
        }

        protected override void ProcessRecord()
        {
            if (_scriptAnalyzer is null)
            {
                return;
            }

            AnalysisResult result;

            try
            {
                if (ParameterSetName == "File")
                {
                    string? resolvedPath = ResolvePath(Path);
                    if (resolvedPath == null)
                    {
                        return;
                    }

                    result = _scriptAnalyzer.AnalyzeScriptPathFull(resolvedPath);
                }
                else
                {
                    result = _scriptAnalyzer.AnalyzeScriptInputFull(ScriptDefinition);
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

            HashSet<string>? severityFilter = BuildSeverityFilter();

            if (SuppressedOnly.IsPresent)
            {
                foreach (SuppressedDiagnostic suppressed in result.SuppressedDiagnostics)
                {
                    SuppressedRecord record = DiagnosticRecord.FromSuppressedDiagnostic(suppressed);
                    if (PassesFilters(record, severityFilter))
                    {
                        WriteObject(record);
                    }
                }
            }
            else
            {
                var records = new List<DiagnosticRecord>();
                foreach (ScriptDiagnostic diagnostic in result.Diagnostics)
                {
                    DiagnosticRecord record = DiagnosticRecord.FromEngineDiagnostic(diagnostic);
                    if (PassesFilters(record, severityFilter))
                    {
                        records.Add(record);
                    }
                }

                records.Sort(static (left, right) => CompareDiagnosticRecords(left, right));
                for (int i = 0; i < records.Count; i++)
                {
                    WriteObject(records[i]);
                }

                if (IncludeSuppressed.IsPresent)
                {
                    foreach (SuppressedDiagnostic suppressed in result.SuppressedDiagnostics)
                    {
                        SuppressedRecord record = DiagnosticRecord.FromSuppressedDiagnostic(suppressed);
                        if (PassesFilters(record, severityFilter))
                        {
                            WriteObject(record);
                        }
                    }
                }
            }

            ReportUnappliedSuppressions(result);
        }

        private void ReportUnappliedSuppressions(AnalysisResult result)
        {
            foreach (RuleSuppression unapplied in result.UnappliedSuppressions)
            {
                string pssaName = RuleNameMapper.ToPssaRuleName(unapplied.RuleName);
                if (pssaName.StartsWith("PS", StringComparison.OrdinalIgnoreCase)
                    && pssaName.Length > 2
                    && !char.IsUpper(pssaName[2]))
                {
                    pssaName = unapplied.RuleName;
                }

                if (!PassesIncludeFilter(pssaName))
                {
                    continue;
                }

                if (IsExcluded(pssaName))
                {
                    continue;
                }

                var unappliedRecord = new { RuleName = pssaName, RuleSuppressionID = unapplied.RuleSuppressionId };

                var errorRecord = new ErrorRecord(
                    new ArgumentException(
                        $"Suppression '{pssaName}' with ID '{unapplied.RuleSuppressionId}' was not applied."),
                    "suppression message attribute error",
                    ErrorCategory.InvalidArgument,
                    unappliedRecord);

                WriteError(errorRecord);
            }
        }

        private bool PassesFilters(DiagnosticRecord record, HashSet<string>? severityFilter)
        {
            if (!PassesIncludeFilter(record.RuleName))
            {
                return false;
            }

            if (IsExcluded(record.RuleName))
            {
                return false;
            }

            if (severityFilter != null && !severityFilter.Contains(record.Severity.ToString()))
            {
                return false;
            }

            return true;
        }

        private static int CompareDiagnosticRecords(DiagnosticRecord left, DiagnosticRecord right)
        {
            int cmp = string.Compare(left.ScriptPath, right.ScriptPath, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = CompareExtents(left.Extent, right.Extent);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = string.Compare(left.RuleName, right.RuleName, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = string.Compare(left.Command, right.Command, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = string.Compare(left.Parameter, right.Parameter, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.Compare(left.Message, right.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareExtents(IScriptExtent? left, IScriptExtent? right)
        {
            if (left is null)
            {
                return right is null ? 0 : 1;
            }

            if (right is null)
            {
                return -1;
            }

            int cmp = left.StartLineNumber.CompareTo(right.StartLineNumber);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = left.StartColumnNumber.CompareTo(right.StartColumnNumber);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = left.EndLineNumber.CompareTo(right.EndLineNumber);
            if (cmp != 0)
            {
                return cmp;
            }

            return left.EndColumnNumber.CompareTo(right.EndColumnNumber);
        }

        private string? ResolvePath(string inputPath)
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

        private HashSet<string>? BuildSeverityFilter()
        {
            if (_effectiveSeverity is null || _effectiveSeverity.Length == 0)
            {
                return null;
            }

            return new HashSet<string>(_effectiveSeverity, StringComparer.OrdinalIgnoreCase);
        }

        private Settings? ResolveSettings()
        {
            object? settingsInput = Settings;

            if (settingsInput is PSObject psObj)
            {
                settingsInput = psObj.BaseObject;
            }

            if (settingsInput == null)
            {
                string? searchPath = ParameterSetName == "File" ? Path : SessionState.Path.CurrentFileSystemLocation.Path;
                if (searchPath != null)
                {
                    var directory = searchPath.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                    if (File.Exists(directory))
                    {
                        directory = System.IO.Path.GetDirectoryName(directory);
                    }

                    if (directory is not null && Directory.Exists(directory))
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
            var configDict = new Dictionary<string, IRuleConfiguration?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in Default.RuleConfiguration)
            {
                if (kvp.Value is not null)
                {
                    configDict[kvp.Key] = kvp.Value;
                }
            }

            Settings? parsedSettings = ResolveSettings();
            if (parsedSettings != null)
            {
                _effectiveIncludeRules = IncludeRule ?? parsedSettings.IncludeRules?.ToArray();
                _effectiveExcludeRules = ExcludeRule ?? parsedSettings.ExcludeRules?.ToArray();
                _effectiveSeverity = Severity ?? parsedSettings.Severities?.ToArray();

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

            string? moduleBase = MyInvocation?.MyCommand?.Module?.ModuleBase;
            if (moduleBase is not null)
            {
                string? assemblyDir = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
                string? dbDir = assemblyDir ?? moduleBase;

                string specterDbPath = System.IO.Path.Combine(dbDir, "Data", "specter.db");
                if (System.IO.File.Exists(specterDbPath))
                {
                    BuiltinCommandDatabase.DefaultDatabasePath = specterDbPath;
                }
            }

            var logger = new PowerShellAnalysisLogger(this);

            var builder = new ScriptAnalyzerBuilder()
                .WithLogger(logger)
                .WithRuleComponentProvider(rcpb => rcpb.UseBuiltinDatabase())
                .WithRuleExecutorFactory(new ParallelRuleExecutorFactory(logger))
                .AddBuiltinRules(configDict);

            string[]? customRulePaths = CustomRulePath ?? parsedSettings?.CustomRulePath?.ToArray();
            bool recurseCustom = RecurseCustomRulePath.IsPresent
                || (parsedSettings?.RecurseCustomRulePath ?? false);

            string? settingsDir = ResolveSettingsDirectory();

            if (customRulePaths is not null)
            {
                foreach (string rulePath in customRulePaths)
                {
                    string resolvedRulePath = ResolveRulePath(rulePath, settingsDir);

                    var factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                        resolvedRulePath,
                        settingsDir,
                        configDict,
                        recurseCustom,
                        skipOwnershipCheck: false,
                        logger);

                    foreach (var factory in factories)
                    {
                        builder.AddRuleProviderFactory(factory);
                    }
                }
            }

            return builder.Build();
        }

        private string? ResolveSettingsDirectory()
        {
            if (Settings is string settingsPath)
            {
                try
                {
                    var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(settingsPath);
                    if (resolved.Count > 0)
                    {
                        return System.IO.Path.GetDirectoryName(resolved[0].Path);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private string ResolveRulePath(string rulePath, string? settingsDir)
        {
            if (System.IO.Path.IsPathRooted(rulePath))
            {
                return rulePath;
            }

            if (settingsDir is not null)
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(settingsDir, rulePath));
            }

            try
            {
                var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(rulePath);
                if (resolved.Count > 0)
                {
                    return resolved[0].Path;
                }
            }
            catch
            {
            }

            return System.IO.Path.GetFullPath(rulePath);
        }

        private static void ApplyRuleArguments(
            Dictionary<string, IRuleConfiguration?> configDict,
            Dictionary<string, Dictionary<string, object>> ruleArguments)
        {
            var ruleConfigMap = BuildRuleConfigMap();
            var allRuleNameMap = BuildAllRuleNameMap();

            foreach (var ruleEntry in ruleArguments)
            {
                string pssaRuleName = ruleEntry.Key;
                Dictionary<string, object> args = ruleEntry.Value;

                if (ruleConfigMap.TryGetValue(pssaRuleName, out var ruleMapping))
                {
                    IRuleConfiguration? config = CreateConfigFromArguments(ruleMapping.ConfigType, args);
                    if (config != null)
                    {
                        configDict[ruleMapping.EngineConfigKey] = config;
                    }
                }
                else if (allRuleNameMap.TryGetValue(pssaRuleName, out string? engineKey))
                {
                    if (args.TryGetValue("Enable", out object? enableVal))
                    {
                        bool enabled = Convert.ToBoolean(enableVal);
                        configDict[engineKey] = new CommonConfiguration(enabled);
                    }
                }
            }
        }

        private static IRuleConfiguration? CreateConfigFromArguments(Type configType, Dictionary<string, object> args)
        {
            try
            {
                object? config = Activator.CreateInstance(configType);
                if (config is null)
                {
                    return null;
                }

                bool hasExplicitEnable = false;
                bool hasNonEnableSettings = false;

                foreach (var arg in args)
                {
                    if (string.Equals(arg.Key, "Enable", StringComparison.OrdinalIgnoreCase))
                    {
                        hasExplicitEnable = true;
                        bool enabled = Convert.ToBoolean(arg.Value);
                        if (config is IEditorConfiguration editorConfig)
                        {
                            editorConfig.Common.Enable = enabled;
                        }
                        else if (config is IRuleConfiguration ruleConfig)
                        {
                            ruleConfig.Common.Enable = enabled;
                        }

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

                    object? convertedValue = ConvertSettingsValue(arg.Value, prop.PropertyType);
                    if (convertedValue != null || !prop.PropertyType.IsValueType)
                    {
                        prop.SetValue(config, convertedValue);
                        hasNonEnableSettings = true;
                    }
                }

                if (hasNonEnableSettings && !hasExplicitEnable)
                {
                    if (config is IEditorConfiguration editorCfg)
                    {
                        editorCfg.Common.Enable = true;
                    }
                    else if (config is IRuleConfiguration ruleCfg)
                    {
                        ruleCfg.Common.Enable = true;
                    }
                }

                return (IRuleConfiguration)config;
            }
            catch
            {
                return null;
            }
        }

        private static object? ConvertSettingsValue(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

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

            if (value is string[] strArray)
            {
                if (typeof(IEnumerable<string>).IsAssignableFrom(targetType))
                {
                    return strArray;
                }
            }

            if (value is string singleStr && targetType == typeof(string[]))
            {
                return new[] { singleStr };
            }

            if (targetType.IsEnum)
            {
                string? strVal = value.ToString();
                if (strVal is not null)
                {
                    try
                    {
                        return Enum.Parse(targetType, strVal, ignoreCase: true);
                    }
                    catch
                    {
                    }
                }

                return null;
            }

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

        private static Dictionary<string, string> BuildAllRuleNameMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Type ruleType in BuiltinRules.DefaultRules)
            {
                if (!RuleInfo.TryGetFromRuleType(ruleType, out RuleInfo? ruleInfo) || ruleInfo is null)
                {
                    continue;
                }

                string pssaName = RuleNameMapper.ToPssaName(ruleInfo.Name);
                map[pssaName] = ruleInfo.FullName;
            }

            return map;
        }

        private static Dictionary<string, RuleConfigMapping> BuildRuleConfigMap()
        {
            var map = new Dictionary<string, RuleConfigMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (Type ruleType in BuiltinRules.DefaultRules)
            {
                if (!RuleInfo.TryGetFromRuleType(ruleType, out RuleInfo? ruleInfo) || ruleInfo is null)
                {
                    continue;
                }

                Type? configType = FindConfigurationType(ruleType);
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

        private static Type? FindConfigurationType(Type ruleType)
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
