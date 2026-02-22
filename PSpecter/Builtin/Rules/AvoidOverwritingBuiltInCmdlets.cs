#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation.Language;
using Newtonsoft.Json.Linq;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace PSpecter.Builtin.Rules
{
    public class AvoidOverwritingBuiltInCmdletsConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enabled: true);

        public string[] PowerShellVersion { get; set; } = Array.Empty<string>();
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("AvoidOverwritingBuiltInCmdlets", typeof(Strings), nameof(Strings.AvoidOverwritingBuiltInCmdletsDescription))]
    public class AvoidOverwritingBuiltInCmdlets : ConfigurableScriptRule<AvoidOverwritingBuiltInCmdletsConfiguration>
    {
        public AvoidOverwritingBuiltInCmdlets(RuleInfo ruleInfo, AvoidOverwritingBuiltInCmdletsConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string fileName)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string[] psVersions = Configuration.PowerShellVersion;
            if (psVersions == null || psVersions.Length == 0 || string.IsNullOrEmpty(psVersions[0]))
            {
#if CORECLR
                psVersions = new[] { "core-6.1.0-windows" };
#else
                psVersions = new[] { "desktop-5.1.14393.206-windows" };
#endif
            }

            string settingsPath = FindSettingsDirectory();
            if (settingsPath == null)
            {
                yield break;
            }

            var cmdletMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string version in psVersions)
            {
                if (version.IndexOfAny(new[] { '/', '\\', ':' }) >= 0
                    || version.Contains(".."))
                {
                    continue;
                }

                string jsonPath = Path.Combine(settingsPath, version + ".json");
                if (!File.Exists(jsonPath))
                {
                    continue;
                }

                if (!cmdletMap.ContainsKey(version))
                {
                    cmdletMap[version] = LoadCmdletsFromJson(jsonPath);
                }
            }

            if (cmdletMap.Count == 0)
            {
                yield break;
            }

            foreach (Ast node in ast.FindAll(testAst => testAst is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;
                string functionName = funcAst.Name;

                foreach (var entry in cmdletMap)
                {
                    if (entry.Value.Contains(functionName))
                    {
                        yield return CreateDiagnostic(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.AvoidOverwritingBuiltInCmdletsError,
                                functionName,
                                entry.Key),
                            funcAst.Extent);
                    }
                }
            }
        }

        private static HashSet<string> LoadCmdletsFromJson(string jsonPath)
        {
            var cmdlets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            JObject data = JObject.Parse(File.ReadAllText(jsonPath));

            JArray modules = data["Modules"] as JArray;
            if (modules == null)
            {
                return cmdlets;
            }

            foreach (JToken module in modules)
            {
                JArray commands = module["ExportedCommands"] as JArray;
                if (commands == null)
                {
                    continue;
                }

                foreach (JToken command in commands)
                {
                    string name = command["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        cmdlets.Add(name);
                    }
                }
            }

            return cmdlets;
        }

        private static string FindSettingsDirectory()
        {
            string assemblyLocation = typeof(AvoidOverwritingBuiltInCmdlets).Assembly.Location;
            if (string.IsNullOrWhiteSpace(assemblyLocation))
            {
                return null;
            }

            string settingsPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), "Settings");
            if (Directory.Exists(settingsPath))
            {
                return settingsPath;
            }

            return null;
        }
    }
}
