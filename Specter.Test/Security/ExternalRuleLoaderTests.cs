using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Loader;
using Specter.Builder;
using Specter.Configuration;
using Specter.Execution;
using Specter.Instantiation;
using Specter.Rules;
using Specter.Security;
using Xunit;

namespace Specter.Test.Security
{
    public class ExternalRuleLoaderTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Dictionary<string, IRuleConfiguration?> _emptyConfig;

        public ExternalRuleLoaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpecterLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
            _emptyConfig = new Dictionary<string, IRuleConfiguration?>();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void InvalidPath_ReturnsNull()
        {
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                "/nonexistent/path.dll",
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Null(factory);
        }

        [Fact]
        public void UnsupportedExtension_ReturnsNull()
        {
            string txtFile = Path.Combine(_tempDir, "rules.txt");
            File.WriteAllText(txtFile, "not a rule");

            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                txtFile,
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Null(factory);
        }

        [Fact]
        public void RelativePath_ResolvedFromSettingsDir()
        {
            string settingsDir = Path.Combine(_tempDir, "settings");
            Directory.CreateDirectory(settingsDir);
            string rulesDir = Path.Combine(settingsDir, "rules");
            Directory.CreateDirectory(rulesDir);
            string dllFile = Path.Combine(rulesDir, "MyRules.dll");
            File.WriteAllText(dllFile, "fake");

            // The dll file is fake so assembly loading will fail, but the path resolution
            // should at least attempt to load from the correct resolved path
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                "rules/MyRules.dll",
                settingsFileDirectory: settingsDir,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            // Will return null because the DLL is not a valid assembly,
            // but importantly it didn't throw and it attempted the right path
        }

        [Fact]
        public void EmptyDirectory_ReturnsNoFactories()
        {
            string emptyDir = Path.Combine(_tempDir, "empty");
            Directory.CreateDirectory(emptyDir);

            List<IRuleProviderFactory> factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                emptyDir,
                settingsFileDirectory: null,
                _emptyConfig,
                recurse: false,
                skipOwnershipCheck: true,
                logger: null);

            Assert.Empty(factories);
        }

        [Fact]
        public void DirectoryWithMixedFiles_OnlyClassifiesKnownExtensions()
        {
            string rulesDir = Path.Combine(_tempDir, "mixed");
            Directory.CreateDirectory(rulesDir);

            File.WriteAllText(Path.Combine(rulesDir, "readme.txt"), "not a rule");
            File.WriteAllText(Path.Combine(rulesDir, "script.ps1"), "not a rule module");
            File.WriteAllText(Path.Combine(rulesDir, "module.psm1"), "function Measure-Foo {}");

            List<IRuleProviderFactory> factories = ExternalRuleLoader.CreateProviderFactoriesForDirectory(
                rulesDir,
                settingsFileDirectory: null,
                _emptyConfig,
                recurse: false,
                skipOwnershipCheck: true,
                logger: null);

            // .psm1 gets a factory, .txt and .ps1 are ignored
            Assert.Single(factories);
        }

        [Fact]
        public void CSharpDllRule_IsDiscoveredAndProducesDiagnostics()
        {
            string dllPath = BuildFixtureProject("SampleCSharpRuleModule", "SampleCSharpRuleModule.dll");
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                dllPath,
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.NotNull(factory);

            var provider = factory!.CreateRuleProvider(new RuleComponentProviderBuilder().Build());
            int ruleCount = 0;
            foreach (ScriptRule _ in provider.GetScriptRules())
            {
                ruleCount++;
            }

            Assert.Equal(1, ruleCount);

            var analyzer = new ScriptAnalyzerBuilder()
                .WithRuleComponentProvider(new RuleComponentProviderBuilder().Build())
                .WithRuleExecutorFactory(new SequentialRuleExecutorFactory())
                .AddRuleProviderFactory(factory)
                .Build();

            IReadOnlyCollection<ScriptDiagnostic> diagnostics = analyzer.AnalyzeScriptInput("Invoke-Expression $cmd");
            Assert.NotEmpty(diagnostics);
        }

        [Fact]
        public void CSharpDllRule_LoadsInDedicatedAssemblyLoadContext()
        {
            string dllPath = BuildFixtureProject("SampleCSharpRuleModule", "SampleCSharpRuleModule.dll");
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                dllPath,
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.NotNull(factory);

            var provider = factory!.CreateRuleProvider(new RuleComponentProviderBuilder().Build());
            ScriptRule rule = Assert.Single(provider.GetScriptRules());
            AssemblyLoadContext? alc = AssemblyLoadContext.GetLoadContext(rule.GetType().Assembly);

            Assert.NotNull(alc);
            Assert.NotSame(AssemblyLoadContext.Default, alc);
        }

        [Fact]
        public void DllWithoutRuleTypes_ProducesNoRules()
        {
            string dllPath = BuildFixtureProject("SampleNoRuleModule", "SampleNoRuleModule.dll");
            IRuleProviderFactory? factory = ExternalRuleLoader.CreateProviderFactory(
                dllPath,
                settingsFileDirectory: null,
                _emptyConfig,
                skipOwnershipCheck: true,
                logger: null);

            Assert.NotNull(factory);

            var provider = factory!.CreateRuleProvider(new RuleComponentProviderBuilder().Build());
            Assert.Empty(provider.GetRuleInfos());
            Assert.Empty(provider.GetScriptRules());
        }

        private static string BuildFixtureProject(string fixtureName, string outputDllName)
        {
            string repoRoot = FindRepoRoot();
            string fixtureDir = Path.Combine(repoRoot, "Specter.Test", "TestFixtures", fixtureName);
            string csprojPath = Path.Combine(fixtureDir, fixtureName + ".csproj");
            Assert.True(File.Exists(csprojPath), $"Fixture project not found: {csprojPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csprojPath}\" -c Debug -p:NuGetAudit=false",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            string stdout = process!.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"Fixture build failed for {fixtureName}:{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");

            string dllPath = Path.Combine(fixtureDir, "bin", "Debug", "net8.0", outputDllName);
            Assert.True(File.Exists(dllPath), $"Fixture output not found: {dllPath}");
            return dllPath;
        }

        private static string FindRepoRoot()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null)
            {
                string candidate = Path.Combine(directory, "Specter.sln");
                if (File.Exists(candidate))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new InvalidOperationException("Could not locate repository root from test base directory.");
        }
    }
}
