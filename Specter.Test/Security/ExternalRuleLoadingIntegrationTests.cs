using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Specter.Builder;
using Specter.Configuration;
using Specter.Instantiation;
using Specter.Logging;
using Specter.Rules;
using Specter.Security;
using Xunit;

namespace Specter.Test.Security
{
    public class ExternalRuleLoadingIntegrationTests
    {
        private static string GetFixturePath(string relativePath)
        {
            string testDir = AppContext.BaseDirectory;
            string fixtureBase = Path.Combine(testDir, "..", "..", "..", "TestFixtures");
            return Path.GetFullPath(Path.Combine(fixtureBase, relativePath));
        }

        [Fact]
        public void PssaRuleModule_ManifestPassesAudit()
        {
            string manifestPath = GetFixturePath("PssaRuleModule/PssaTestRules.psd1");
            Assert.True(File.Exists(manifestPath), $"Fixture not found: {manifestPath}");

            ManifestAuditResult result = ModuleManifestAuditor.Audit(manifestPath, logger: null);
            Assert.True(result.IsValid, $"Clean PSSA rule module should pass audit. Reason: {result.RejectionReason}");
        }

        [Fact]
        public void DangerousModule_ManifestIsRejected()
        {
            string manifestPath = GetFixturePath("DangerousModule/Dangerous.psd1");
            Assert.True(File.Exists(manifestPath), $"Fixture not found: {manifestPath}");

            ManifestAuditResult result = ModuleManifestAuditor.Audit(manifestPath, logger: null);
            Assert.False(result.IsValid);
            Assert.Contains("ScriptsToProcess", result.RejectionReason);
        }

        [Fact]
        public void DangerousModule_ProviderFactoryReturnsEmpty()
        {
            string manifestPath = GetFixturePath("DangerousModule/Dangerous.psd1");
            Assert.True(File.Exists(manifestPath), $"Fixture not found: {manifestPath}");

            var factory = new PSModuleRuleProviderFactory(manifestPath, NullAnalysisLogger.Instance);
            var componentProvider = new RuleComponentProviderBuilder().Build();

            IRuleProvider provider = factory.CreateRuleProvider(componentProvider);
            Assert.Empty(provider.GetRuleInfos());
        }

        [Fact(Skip = "Requires full PowerShell runtime; run via Pester integration tests")]
        public void PssaRuleModule_DiscoversMeasureFunction()
        {
            string modulePath = GetFixturePath("PssaRuleModule/PssaTestRules.psd1");
            Assert.True(File.Exists(modulePath), $"Fixture not found: {modulePath}");

            ManifestAuditResult auditResult = ModuleManifestAuditor.Audit(modulePath, logger: null);
            Assert.True(auditResult.IsValid);

            var runspace = ConstrainedRuleRunspaceFactory.CreateConstrainedRunspace(NullAnalysisLogger.Instance);
            try
            {
                ConstrainedRuleRunspaceFactory.ImportModuleAndLockDown(
                    runspace, modulePath, NullAnalysisLogger.Instance);

                List<DiscoveredPSRule> discovered = PSRuleDiscovery.DiscoverRules(
                    runspace, NullAnalysisLogger.Instance);

                Assert.NotEmpty(discovered);
                Assert.Contains(discovered, r =>
                    r.FunctionName.Equals("Measure-EmptyDescription", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(discovered, r => r.Convention == PSRuleConvention.PssaLegacy);
            }
            finally
            {
                runspace.Dispose();
            }
        }

        [Fact]
        public void LegacyDiagnosticRecordModule_ManifestPassesAudit()
        {
            string manifestPath = GetFixturePath("LegacyDiagnosticRecordModule/LegacyTestRules.psd1");
            Assert.True(File.Exists(manifestPath), $"Fixture not found: {manifestPath}");

            ManifestAuditResult result = ModuleManifestAuditor.Audit(manifestPath, logger: null);
            Assert.True(result.IsValid, $"Legacy module should pass audit. Reason: {result.RejectionReason}");
        }

        [Fact]
        public void LocalizedRuleModule_ManifestPassesAudit()
        {
            string manifestPath = GetFixturePath("LocalizedRuleModule/LocalizedTestRules.psd1");
            Assert.True(File.Exists(manifestPath), $"Fixture not found: {manifestPath}");

            ManifestAuditResult result = ModuleManifestAuditor.Audit(manifestPath, logger: null);
            Assert.True(result.IsValid, $"Localized module should pass audit. Reason: {result.RejectionReason}");
        }

        [Fact]
        public void AddRulesFromPath_RequiresAbsolutePath()
        {
            var builder = new ScriptAnalyzerBuilder();
            Assert.Throws<ArgumentException>(() =>
                builder.AddRulesFromPath("relative/path.dll"));
        }

        [Fact]
        public void AddRulesFromModule_RequiresAbsolutePath()
        {
            var builder = new ScriptAnalyzerBuilder();
            Assert.Throws<ArgumentException>(() =>
                builder.AddRulesFromModule("relative/module.psm1"));
        }

        [Fact]
        public void PathValidation_TraversalRejected()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SpecterIntTest_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            try
            {
                string subDir = Path.Combine(tempDir, "allowed");
                Directory.CreateDirectory(subDir);

                string traversalPath = Path.Combine(subDir, "..", "..", "etc", "passwd.dll");
                RulePathValidationResult result = RulePathValidator.ValidatePath(
                    traversalPath, allowedRoot: subDir, logger: null);

                Assert.False(result.IsValid);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ExternalRuleLoader_NonexistentPath_HandledGracefully()
        {
            var emptyConfig = new Dictionary<string, IRuleConfiguration?>();
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                "/absolutely/nonexistent/path.dll",
                settingsFileDirectory: null,
                emptyConfig,
                skipOwnershipCheck: true,
                logger: NullAnalysisLogger.Instance);

            Assert.Null(factory);
        }

        [Fact]
        public void ExternalRuleLoader_SettingsRelativeResolution()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SpecterSettingsTest_" + Guid.NewGuid().ToString("N")[..8]);
            string rulesDir = Path.Combine(tempDir, "rules");
            Directory.CreateDirectory(rulesDir);

            string psm1 = Path.Combine(rulesDir, "test.psm1");
            File.WriteAllText(psm1, "function Measure-Test { param($ScriptBlockAst) }");

            try
            {
                var emptyConfig = new Dictionary<string, IRuleConfiguration?>();
                IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                    "rules/test.psm1",
                    settingsFileDirectory: tempDir,
                    emptyConfig,
                    skipOwnershipCheck: true,
                    logger: NullAnalysisLogger.Instance);

                // Should find the file via settings-relative resolution
                Assert.NotNull(factory);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
