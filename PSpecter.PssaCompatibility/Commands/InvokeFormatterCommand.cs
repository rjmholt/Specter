using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using PSpecter.CommandDatabase;
using PSpecter.Formatting;
using PSpecter.Module.CommandDatabase;
using Microsoft.Windows.PowerShell.ScriptAnalyzer;

namespace PSpecter.PssaCompatibility.Commands
{
    [Cmdlet(VerbsLifecycle.Invoke, "Formatter")]
    [OutputType(typeof(string))]
    public class InvokeFormatterCommand : PSCmdlet
    {
        private const string DefaultSettingsPreset = "CodeFormatting";

        private ScriptFormatter? _formatter;

        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public string ScriptDefinition { get; set; } = string.Empty;

        [Parameter(
            Mandatory = false,
            Position = 1,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public object Settings { get; set; } = DefaultSettingsPreset;

        [Parameter(
            Mandatory = false,
            Position = 2,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        [ValidateCount(4, 4)]
        public int[]? Range { get; set; }

        protected override void BeginProcessing()
        {
            IReadOnlyDictionary<string, IEditorConfiguration>? configs;

            try
            {
                configs = ResolveConfiguration();
            }
            catch (Exception e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    e,
                    "SETTINGS_ERROR",
                    ErrorCategory.InvalidData,
                    Settings));
                return;
            }

            if (configs == null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException(string.Format(
                        CultureInfo.CurrentCulture,
                        "Settings could not be parsed.")),
                    "SETTINGS_ERROR",
                    ErrorCategory.InvalidArgument,
                    Settings));
                return;
            }

            var services = new Dictionary<Type, object>
            {
                [typeof(IPowerShellCommandDatabase)] = SessionStateCommandDatabase.Create(SessionState.InvokeCommand)
            };
            _formatter = ScriptFormatter.FromEditorConfigs(configs, services);
        }

        protected override void ProcessRecord()
        {
            if (_formatter is null)
            {
                return;
            }

            string formatted = _formatter.Format(ScriptDefinition);
            WriteObject(formatted);
        }

        private IReadOnlyDictionary<string, IEditorConfiguration>? ResolveConfiguration()
        {
            object settingsInput = Settings;

            if (settingsInput is PSObject psObj)
            {
                settingsInput = psObj.BaseObject;
            }

            if (settingsInput is string settingsString)
            {
                if (FormatterSettingsConverter.TryGetPreset(settingsString, out var preset))
                {
                    return preset;
                }

                string? resolvedPath = ResolveSettingsPath(settingsString);
                if (resolvedPath != null)
                {
                    var settings = new Microsoft.Windows.PowerShell.ScriptAnalyzer.Settings(resolvedPath);
                    return FormatterSettingsConverter.FromSettings(settings);
                }

                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    "Cannot find settings preset or file: '{0}'.",
                    settingsString));
            }

            if (settingsInput is Hashtable hashtable)
            {
                var settings = new Microsoft.Windows.PowerShell.ScriptAnalyzer.Settings(hashtable);
                return FormatterSettingsConverter.FromSettings(settings);
            }

            throw new ArgumentException(string.Format(
                CultureInfo.CurrentCulture,
                "Settings must be a string (preset name or file path) or a hashtable. Got: {0}",
                settingsInput?.GetType().Name ?? "null"));
        }

        private string? ResolveSettingsPath(string path)
        {
            try
            {
                var resolved = SessionState.Path.GetResolvedPSPathFromPSPath(path);
                if (resolved.Count > 0)
                {
                    return resolved[0].Path;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
