using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Specter.Rules;
using Specter.Tools;

namespace Specter.Rules.Builtin.Rules
{
    [IdempotentRule]
    [ThreadsafeRule]
    [Rule("AvoidUsingDeprecatedManifestFields", typeof(Strings), nameof(Strings.AvoidUsingDeprecatedManifestFieldsDescription))]
    internal class AvoidUsingDeprecatedManifestFields : ScriptRule
    {
        private static readonly string[] s_deprecatedFields = new[] { "ModuleToProcess" };

        internal AvoidUsingDeprecatedManifestFields(RuleInfo ruleInfo)
            : base(ruleInfo)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast is null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            if (scriptPath is null || !AstExtensions.IsModuleManifest(scriptPath))
            {
                yield break;
            }

            HashtableAst? hashtable = ast
                .FindAll(static node => node is HashtableAst, searchNestedScriptBlocks: false)
                .OfType<HashtableAst>()
                .FirstOrDefault();

            if (hashtable is null)
            {
                yield break;
            }

            if (HasPowerShellVersionBelow3(hashtable))
            {
                yield break;
            }

            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyAst)
                {
                    foreach (string deprecated in s_deprecatedFields)
                    {
                        if (string.Equals(keyAst.Value, deprecated, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return CreateDiagnostic(
                                Strings.AvoidUsingDeprecatedManifestFieldsDescription,
                                kvp.Item1);
                        }
                    }
                }
            }
        }

        private static bool HasPowerShellVersionBelow3(HashtableAst hashtable)
        {
            foreach (var kvp in hashtable.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyAst
                    && string.Equals(keyAst.Value, "PowerShellVersion", StringComparison.OrdinalIgnoreCase)
                    && kvp.Item2 is not null)
                {
                    var valueAst = kvp.Item2.Find(node => node is StringConstantExpressionAst, searchNestedScriptBlocks: false);
                    if (valueAst is StringConstantExpressionAst versionAst
                        && Version.TryParse(versionAst.Value, out Version? version)
                        && version is not null && version.Major < 3)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
