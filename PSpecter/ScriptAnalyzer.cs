using PSpecter.Builder;
using PSpecter.Execution;
using PSpecter.Instantiation;
using PSpecter.Rules;
using PSpecter.Suppression;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSpecter
{
    public class ScriptAnalyzer
    {
        public static ScriptAnalyzer Create(
            RuleComponentProvider ruleComponentProvider,
            IRuleExecutorFactory executorFactory,
            IReadOnlyList<IRuleProviderFactory> ruleProviderFactories)
        {
            var ruleProviders = new List<IRuleProvider>(ruleProviderFactories.Count);
            foreach (IRuleProviderFactory ruleProviderFactory in ruleProviderFactories)
            {
                ruleProviders.Add(ruleProviderFactory.CreateRuleProvider(ruleComponentProvider));
            }

            return new ScriptAnalyzer(executorFactory, ruleProviders);
        }

        private readonly IRuleExecutorFactory _executorFactory;

        private ScriptAnalyzer(
            IRuleExecutorFactory executorFactory,
            IReadOnlyList<IRuleProvider> ruleProviders)
        {
            RuleProviders = ruleProviders;
            _executorFactory = executorFactory;
        }

        public IReadOnlyList<IRuleProvider> RuleProviders { get; }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptPath(string path)
        {
            Ast ast = Parser.ParseFile(path, out Token[] tokens, out ParseError[] parseErrors);
            return AnalyzeScript(ast, tokens, path);
        }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptInput(string input)
        {
            Ast ast = Parser.ParseInput(input, out Token[] tokens, out ParseError[] parseErrors);
            return AnalyzeScript(ast, tokens, scriptPath: null);
        }

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScript(Ast scriptAst, Token[] scriptTokens) =>
            AnalyzeScript(scriptAst, scriptTokens, scriptPath: null);

        public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScript(Ast scriptAst, Token[] scriptTokens, string? scriptPath)
        {
            IRuleExecutor ruleExecutor = _executorFactory.CreateRuleExecutor(scriptAst, scriptTokens, scriptPath);

            foreach (IRuleProvider ruleProvider in RuleProviders)
            {
                foreach (ScriptRule rule in ruleProvider.GetScriptRules())
                {
                    ruleExecutor.AddRule(rule);
                }
            }

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = ruleExecutor.CollectDiagnostics();

            Dictionary<string, List<RuleSuppression>> suppressions = SuppressionParser.GetSuppressions(scriptAst);
            return SuppressionApplier.ApplySuppressions(diagnostics, suppressions);
        }
    }
}
