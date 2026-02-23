using System.Collections.Generic;
using System.IO;
using System.Linq;
using Specter;
using Specter.Builder;
using Specter.Builtin.Rules;
using Specter.Execution;
using Xunit;

namespace Specter.Test.Rules
{
    public class UseBOMForUnicodeEncodedFileTests
    {
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UseBOMForUnicodeEncodedFileTests()
        {
            _scriptAnalyzer = new ScriptAnalyzerBuilder()
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .AddRules(ruleProvider => ruleProvider.AddRule<UseBOMForUnicodeEncodedFile>())
                .Build();
        }

        [Fact]
        public void AsciiOnlyScript_ShouldNotReturnViolation()
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, "Get-Process");

                IReadOnlyList<ScriptDiagnostic> violations = _scriptAnalyzer.AnalyzeScriptPath(tempPath).ToList();

                Assert.Empty(violations);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
