using Specter.Execution;
using Specter.Logging;
using Specter.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Xunit;

namespace Specter.Test.Execution
{
    public class RuleExecutorConcurrencyTests
    {
        [Fact]
        public void ParallelExecutor_CollectsAllParallelRuleErrors()
        {
            Ast ast = Parser.ParseInput("Write-Output 'test'", out Token[] tokens, out _);
            var executor = new ParallelRuleExecutor(ast, tokens, scriptPath: null, NullAnalysisLogger.Instance, maxDegreeOfParallelism: Environment.ProcessorCount);

            for (int i = 0; i < 64; i++)
            {
                executor.AddRule(new FailingThreadsafeRule());
            }

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = executor.CollectDiagnostics();
            Assert.Empty(diagnostics);
            Assert.Equal(64, executor.Errors.Count);
        }

        [Fact]
        public void ParallelExecutor_RespectsSequentialRulesAndDegreeOfParallelism()
        {
            Ast ast = Parser.ParseInput("Write-Output 'test'", out Token[] tokens, out _);
            var executor = new ParallelRuleExecutor(ast, tokens, scriptPath: null, NullAnalysisLogger.Instance, maxDegreeOfParallelism: 1);

            executor.AddRule(new DiagnosticThreadsafeRule());
            executor.AddRule(new DiagnosticThreadsafeRule());
            executor.AddRule(new DiagnosticSequentialRule());

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = executor.CollectDiagnostics();
            Assert.Equal(3, diagnostics.Count);
            Assert.Empty(executor.Errors);
        }

        [ThreadsafeRule]
        [Rule("FailingThreadsafeRule", "Test rule")]
        private sealed class FailingThreadsafeRule : ScriptRule
        {
            private static readonly RuleInfo s_ruleInfo = GetRuleInfo(typeof(FailingThreadsafeRule));

            public FailingThreadsafeRule()
                : base(s_ruleInfo)
            {
            }

            public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
            {
                throw new InvalidOperationException("Boom");
            }
        }

        [ThreadsafeRule]
        [Rule("DiagnosticThreadsafeRule", "Test rule")]
        private sealed class DiagnosticThreadsafeRule : ScriptRule
        {
            private static readonly RuleInfo s_ruleInfo = GetRuleInfo(typeof(DiagnosticThreadsafeRule));

            public DiagnosticThreadsafeRule()
                : base(s_ruleInfo)
            {
            }

            public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
            {
                yield return CreateDiagnostic("Threadsafe", ast);
            }
        }

        [Rule("DiagnosticSequentialRule", "Test rule")]
        private sealed class DiagnosticSequentialRule : ScriptRule
        {
            private static readonly RuleInfo s_ruleInfo = GetRuleInfo(typeof(DiagnosticSequentialRule));

            public DiagnosticSequentialRule()
                : base(s_ruleInfo)
            {
            }

            public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
            {
                yield return CreateDiagnostic("Sequential", ast);
            }
        }

        private static RuleInfo GetRuleInfo(Type ruleType)
        {
            Assert.True(RuleInfo.TryGetFromRuleType(ruleType, out RuleInfo? ruleInfo));
            return ruleInfo!;
        }
    }
}
