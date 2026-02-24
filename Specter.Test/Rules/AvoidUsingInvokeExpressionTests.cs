using System.Collections.Generic;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Rules.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUsingInvokeExpressionTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUsingInvokeExpressionTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<AvoidUsingInvokeExpression, AvoidUsingInvokeExpressionConfiguration>(
                        new AvoidUsingInvokeExpressionConfiguration()))
                .Build();
        }

        [Fact]
        public void InvokeExpression_ShouldReturnViolation()
        {
            var script = @"Invoke-Expression 'Get-Process'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUsingInvokeExpression", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Warning, violation.Severity);
        }

        [Fact]
        public void IexAlias_ShouldReturnViolation()
        {
            var script = @"iex 'Get-Process'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void BothInvokeExpressionAndAlias_ShouldReturnTwoViolations()
        {
            var script = @"
Invoke-Expression 'Invoke me'
iex 'Invoke me'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Equal(2, violations.Count);
        }

        [Fact]
        public void NoInvokeExpression_ShouldNotReturnViolation()
        {
            var script = @"Get-Process | Where-Object { $_.CPU -gt 100 }";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void InvokeExpressionCaseInsensitive_ShouldReturnViolation()
        {
            var script = @"INVOKE-EXPRESSION 'test'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void AllowConstantArguments_ConstantString_ShouldNotReturnViolation()
        {
            var analyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<AvoidUsingInvokeExpression, AvoidUsingInvokeExpressionConfiguration>(
                        new AvoidUsingInvokeExpressionConfiguration { AllowConstantArguments = true }))
                .Build();

            var script = @"Invoke-Expression 'Get-Process'";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void AllowConstantArguments_Variable_ShouldReturnViolation()
        {
            var analyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<AvoidUsingInvokeExpression, AvoidUsingInvokeExpressionConfiguration>(
                        new AvoidUsingInvokeExpressionConfiguration { AllowConstantArguments = true }))
                .Build();

            var script = @"Invoke-Expression $cmd";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void AllowConstantArguments_ExpandableString_ShouldReturnViolation()
        {
            var analyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider =>
                    ruleProvider.AddRule<AvoidUsingInvokeExpression, AvoidUsingInvokeExpressionConfiguration>(
                        new AvoidUsingInvokeExpressionConfiguration { AllowConstantArguments = true }))
                .Build();

            var script = "Invoke-Expression \"Get-Process $name\"";

            IReadOnlyList<ScriptDiagnostic> violations = analyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ErrorMessage_ShouldContainStableGuidance()
        {
            var script = @"Invoke-Expression 'Get-Process'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("Invoke-Expression is used", violation.Message);
            Assert.Contains("invocation operator", violation.Message);
        }
    }
}
