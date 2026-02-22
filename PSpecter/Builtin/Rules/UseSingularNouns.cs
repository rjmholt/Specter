using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation.Language;
using Pluralize.NET;
using PSpecter.Configuration;
using PSpecter.Rules;
using PSpecter.Tools;

namespace PSpecter.Builtin.Rules
{
    public class UseSingularNounsConfiguration : IRuleConfiguration
    {
        public CommonConfiguration Common { get; set; } = new CommonConfiguration(enabled: true);

        public string[] NounAllowList { get; set; } = { "Data", "Windows" };
    }

    [ThreadsafeRule]
    [IdempotentRule]
    [Rule("UseSingularNouns", typeof(Strings), nameof(Strings.UseSingularNounsDescription))]
    public class UseSingularNouns : ConfigurableScriptRule<UseSingularNounsConfiguration>
    {
        private static readonly Pluralizer s_pluralizer = new Pluralizer();

        public UseSingularNouns(RuleInfo ruleInfo, UseSingularNounsConfiguration configuration)
            : base(ruleInfo, configuration)
        {
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
        {
            if (ast == null)
            {
                throw new ArgumentNullException(nameof(ast));
            }

            var allowList = new HashSet<string>(
                Configuration.NounAllowList ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (Ast node in ast.FindAll(a => a is FunctionDefinitionAst, searchNestedScriptBlocks: true))
            {
                var funcAst = (FunctionDefinitionAst)node;

                if (funcAst.Name == null || !funcAst.Name.Contains('-'))
                {
                    continue;
                }

                string? noun = GetLastWordInCmdlet(funcAst.Name);
                if (noun is null)
                {
                    continue;
                }

                if (!s_pluralizer.IsSingular(noun) && s_pluralizer.IsPlural(noun))
                {
                    if (allowList.Contains(noun))
                    {
                        continue;
                    }

                    IScriptExtent extent = funcAst.GetFunctionNameExtent(tokens) ?? funcAst.Extent;

                    string singularNoun = s_pluralizer.Singularize(noun);
                    string newName = funcAst.Name.Substring(0, funcAst.Name.Length - noun.Length) + singularNoun;
                    var correction = new Correction(extent, newName, $"Singularized correction of '{extent.Text}'");

                    var diagnostic = CreateDiagnostic(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UseSingularNounsError,
                            funcAst.Name),
                        extent,
                        new[] { correction });
                    diagnostic.RuleSuppressionId = funcAst.Name;
                    yield return diagnostic;
                }
            }
        }

        private static string? GetLastWordInCmdlet(string? cmdletName)
        {
            if (string.IsNullOrEmpty(cmdletName))
            {
                return null;
            }

            if (!char.IsLower(cmdletName[cmdletName.Length - 1]))
            {
                return null;
            }

            for (int i = cmdletName.Length - 1; i >= 0; i--)
            {
                if (cmdletName[i] == '-')
                {
                    return null;
                }

                if (char.IsUpper(cmdletName[i]))
                {
                    return cmdletName.Substring(i);
                }
            }

            return null;
        }
    }
}
