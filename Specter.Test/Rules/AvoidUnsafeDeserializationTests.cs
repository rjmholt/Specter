using System.Collections.Generic;
using System.Linq;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class AvoidUnsafeDeserializationTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public AvoidUnsafeDeserializationTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<AvoidUnsafeDeserialization>())
                .Build();
        }

        [Fact]
        public void ImportClixmlWithVariable_ShouldReturnViolation()
        {
            var script = @"Import-Clixml -Path $filePath";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Equal("AvoidUnsafeDeserialization", violation.Rule!.Name);
            Assert.Equal(DiagnosticSeverity.Information, violation.Severity);
            Assert.Contains("Import-Clixml", violation.Message);
        }

        [Fact]
        public void ImportClixmlWithConstant_ShouldNotReturnViolation()
        {
            var script = @"Import-Clixml -Path 'C:\data\safe.xml'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void PSSerializerDeserializeWithVariable_ShouldReturnViolation()
        {
            var script = @"[PSSerializer]::Deserialize($xmlData)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            ScriptDiagnostic violation = Assert.Single(violations);
            Assert.Contains("PSSerializer", violation.Message);
        }

        [Fact]
        public void PSSerializerDeserializeAsListWithVariable_ShouldReturnViolation()
        {
            var script = @"[PSSerializer]::DeserializeAsList($xmlData)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void PSSerializerDeserializeWithConstant_ShouldNotReturnViolation()
        {
            var script = @"[PSSerializer]::Deserialize('<Objs />')";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void XmlDocumentLoadWithVariable_ShouldReturnViolation()
        {
            var script = @"[System.Xml.XmlDocument]::new().Load($path)";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            // This is an instance method, not a static call, so the rule won't flag it
            Assert.Empty(violations);
        }

        [Fact]
        public void NoDeserialization_ShouldNotReturnViolation()
        {
            var script = @"Get-Content -Path $file | ConvertFrom-Json";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }

        [Fact]
        public void ImportClixmlPositionalVariable_ShouldReturnViolation()
        {
            var script = @"Import-Clixml $path";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Single(violations);
        }

        [Fact]
        public void ImportClixmlPositionalConstant_ShouldNotReturnViolation()
        {
            var script = @"Import-Clixml 'config.xml'";

            IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptInput(script).ToList();

            Assert.Empty(violations);
        }
    }
}
