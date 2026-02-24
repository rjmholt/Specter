using Specter.Builder;
using Specter.Configuration;
using System;
using System.Collections.Concurrent;
using System.Management.Automation;

#if !CORECLR
using Specter.Internal;
#endif

namespace Specter.Module.Commands
{
    [Cmdlet(VerbsLifecycle.Invoke, "Specter")]
    public class InvokeScriptAnalyzerCommand : Cmdlet
    {
        private static readonly ConcurrentDictionary<ParameterSetting, ScriptAnalyzer> s_configuredScriptAnalyzers = new ConcurrentDictionary<ParameterSetting, ScriptAnalyzer>();

        private ScriptAnalyzer? _scriptAnalyzer;

        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "FilePath")]
        public string[]? Path { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Input")]
        public string[]? ScriptDefinition { get; set; }

        [Parameter]
        public string? ConfigurationPath { get; set; }

        [Parameter]
        public string[]? ExcludeRules { get; set; }

        [Parameter]
        public string[]? CustomRulePath { get; set; }

        protected override void BeginProcessing()
        {
            _scriptAnalyzer = GetScriptAnalyzer();
        }

        protected override void ProcessRecord()
        {
            if (_scriptAnalyzer is null)
            {
                return;
            }

            if (Path != null)
            {
                foreach (string path in Path)
                {
                    foreach (ScriptDiagnostic diagnostic in _scriptAnalyzer.AnalyzeScriptPath(path))
                    {
                        WriteObject(diagnostic);
                    }
                }

                return;
            }

            if (ScriptDefinition != null)
            {
                foreach (string input in ScriptDefinition)
                {
                    foreach (ScriptDiagnostic diagnostic in _scriptAnalyzer.AnalyzeScriptInput(input))
                    {
                        WriteObject(diagnostic);
                    }
                }
            }
        }

        private ScriptAnalyzer GetScriptAnalyzer()
        {
            var parameters = new ParameterSetting(this);
            return s_configuredScriptAnalyzers.GetOrAdd(parameters, CreateScriptAnalyzerWithParameters);
        }

        private ScriptAnalyzer CreateScriptAnalyzerWithParameters(ParameterSetting parameters)
        {
            var configBuilder = new ScriptAnalyzerConfigurationBuilder()
                .WithBuiltinRuleSet(BuiltinRulePreference.Default);

            if (parameters.ConfigurationPath != null)
            {
                configBuilder.AddConfigurationFile(parameters.ConfigurationPath);
            }

            if (parameters.CustomRulePath is { Length: > 0 })
            {
                configBuilder.AddRulePaths(parameters.CustomRulePath);
            }

            return configBuilder.Build().CreateScriptAnalyzer();
        }

        private struct ParameterSetting
        {
            public ParameterSetting(InvokeScriptAnalyzerCommand command)
            {
                ConfigurationPath = command.ConfigurationPath;
                CustomRulePath = command.CustomRulePath;
            }

            public string? ConfigurationPath { get; }

            public string[]? CustomRulePath { get; }

            public override int GetHashCode()
            {
                string customRuleSignature = string.Empty;
                if (CustomRulePath is { Length: > 0 })
                {
                    var signatureBuilder = new System.Text.StringBuilder();
                    for (int i = 0; i < CustomRulePath.Length; i++)
                    {
                        if (i > 0)
                        {
                            signatureBuilder.Append('|');
                        }

                        signatureBuilder.Append(CustomRulePath[i] ?? string.Empty);
                    }

                    customRuleSignature = signatureBuilder.ToString();
                }

#if CORECLR
                return HashCode.Combine(ConfigurationPath, customRuleSignature);
#else
                return HashCodeCombinator.Create()
                    .Add(ConfigurationPath!)
                    .Add(customRuleSignature)
                    .GetHashCode();
#endif
            }
        }
    }
}
