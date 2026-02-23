using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Language;
using PSpecter;
using PSpecter.Builtin.Rules;
using PSpecter.CommandDatabase;
using PSpecter.CommandDatabase.Import;
using PSpecter.Configuration;
using PSpecter.Rules;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules
{
    public record UseCompatibleCmdletsConfiguration : IRuleConfiguration
    {
        public string[] Compatibility { get; init; } = Array.Empty<string>();
        public string? Reference { get; init; }
        public CommonConfiguration Common { get; init; } = new CommonConfiguration(enabled: false);
    }

    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("UseCompatibleCmdlets", typeof(Strings), nameof(Strings.UseCompatibleCmdletsDescription))]
    public class UseCompatibleCmdlets : ConfigurableScriptRule<UseCompatibleCmdletsConfiguration>
    {
        private const string DefaultReference = "desktop-5.1.14393.206-windows";
        private const string AlternativeDefaultReference = "core-6.1.0-windows";

        private readonly IPowerShellCommandDatabase _commandDb;

        public UseCompatibleCmdlets(
            RuleInfo ruleInfo,
            UseCompatibleCmdletsConfiguration configuration,
            IPowerShellCommandDatabase commandDb)
            : base(ruleInfo, configuration)
        {
            _commandDb = commandDb;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            string[] compatibility = Configuration.Compatibility;
            if (compatibility is null || compatibility.Length == 0)
            {
                yield break;
            }

            var targetPlatforms = new List<(string Label, PlatformInfo Platform)>();
            foreach (string platformStr in compatibility)
            {
                if (LegacySettingsImporter.TryParsePlatformFromFileName(platformStr, out PlatformInfo? platform)
                    && platform is not null)
                {
                    targetPlatforms.Add((platformStr, platform));
                }
            }

            if (targetPlatforms.Count == 0)
            {
                yield break;
            }

            string? referenceStr = Configuration.Reference;
            if (string.IsNullOrEmpty(referenceStr))
            {
                referenceStr = DefaultReference;
                if (targetPlatforms.Count == 1
                    && string.Equals(compatibility[0], DefaultReference, StringComparison.OrdinalIgnoreCase))
                {
                    referenceStr = AlternativeDefaultReference;
                }
            }

            if (!LegacySettingsImporter.TryParsePlatformFromFileName(referenceStr!, out PlatformInfo? refPlatform)
                || refPlatform is null)
            {
                yield break;
            }

            var referencePlatforms = new HashSet<PlatformInfo> { refPlatform };

            foreach (Ast foundAst in ast.FindAll(node => node is CommandAst, searchNestedScriptBlocks: true))
            {
                var cmdAst = (CommandAst)foundAst;
                string commandName = cmdAst.GetCommandName();
                if (string.IsNullOrEmpty(commandName))
                {
                    continue;
                }

                bool existsOnReference = _commandDb.CommandExistsOnPlatform(commandName, referencePlatforms);

                foreach ((string label, PlatformInfo platform) in targetPlatforms)
                {
                    var platforms = new HashSet<PlatformInfo> { platform };
                    bool existsOnTarget = _commandDb.CommandExistsOnPlatform(commandName, platforms);

                    if (existsOnReference && !existsOnTarget)
                    {
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseCompatibleCmdletsError,
                            commandName,
                            platform.Edition,
                            platform.Version,
                            platform.Os.Family);

                        yield return CreateDiagnostic(message, cmdAst);
                    }
                }
            }
        }
    }
}
