using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Specter.Configuration;
using Specter.Instantiation;
using Specter.Logging;

namespace Specter.Security
{
    /// <summary>
    /// Orchestrates the secure external rule loading pipeline:
    /// path validation -> ownership check -> classify -> load (assembly or module).
    /// </summary>
    internal static class ExternalRuleLoader
    {
        internal static IRuleProviderFactory? CreateProviderFactory(
            string rawPath,
            string? settingsFileDirectory,
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfiguration,
            bool skipOwnershipCheck,
            IAnalysisLogger? logger)
        {
            string resolvedPath = ResolvePath(rawPath, settingsFileDirectory);

            RulePathValidationResult validation = RulePathValidator.ValidatePath(
                resolvedPath,
                allowedRoot: null,
                logger);

            if (!validation.IsValid)
            {
                logger?.Warning($"Rejected rule path '{rawPath}': {validation.RejectionReason}");
                return null;
            }

            if (!skipOwnershipCheck)
            {
                OwnershipCheckResult ownershipResult = FileOwnershipValidator.ValidateFileAndParents(
                    validation.CanonicalPath!,
                    stopAtRoot: settingsFileDirectory,
                    logger);

                if (!ownershipResult.IsValid)
                {
                    logger?.Warning(
                        $"Rule path '{validation.CanonicalPath}' failed ownership check: {ownershipResult.RejectionReason}. " +
                        "Set ExternalRules to 'unrestricted' to bypass ownership checks.");
                    return null;
                }
            }
            else
            {
                logger?.Warning("Ownership check skipped for rule loading. This reduces security guarantees.");
            }

            return CreateFactoryForKind(validation.CanonicalPath!, validation.Kind, ruleConfiguration, logger);
        }

        internal static List<IRuleProviderFactory> CreateProviderFactoriesForDirectory(
            string rawPath,
            string? settingsFileDirectory,
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfiguration,
            bool recurse,
            bool skipOwnershipCheck,
            IAnalysisLogger? logger)
        {
            var factories = new List<IRuleProviderFactory>();

            string resolvedPath = ResolvePath(rawPath, settingsFileDirectory);

            RulePathValidationResult validation = RulePathValidator.ValidatePath(
                resolvedPath,
                allowedRoot: null,
                logger);

            if (!validation.IsValid)
            {
                logger?.Warning($"Rejected rule path '{rawPath}': {validation.RejectionReason}");
                return factories;
            }

            if (validation.Kind != RuleFileKind.Directory)
            {
                IRuleProviderFactory? single = CreateProviderFactory(
                    rawPath, settingsFileDirectory, ruleConfiguration, skipOwnershipCheck, logger);
                if (single is not null)
                {
                    factories.Add(single);
                }
                return factories;
            }

            var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files;
            try
            {
                files = Directory.GetFiles(validation.CanonicalPath!, "*.*", searchOption);
            }
            catch (Exception ex)
            {
                logger?.Warning($"Failed to enumerate directory '{validation.CanonicalPath}': {ex.Message}");
                return factories;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                RuleFileKind kind = RulePathValidator.ClassifyByExtension(file);
                if (kind == RuleFileKind.Unknown)
                {
                    continue;
                }

                if (!skipOwnershipCheck)
                {
                    OwnershipCheckResult ownershipResult = FileOwnershipValidator.ValidateFileAndParents(
                        file,
                        stopAtRoot: validation.CanonicalPath,
                        logger);

                    if (!ownershipResult.IsValid)
                    {
                        logger?.Warning($"Skipping '{file}': {ownershipResult.RejectionReason}");
                        continue;
                    }
                }

                IRuleProviderFactory? factory = CreateFactoryForKind(file, kind, ruleConfiguration, logger);
                if (factory is not null)
                {
                    factories.Add(factory);
                }
            }

            return factories;
        }

        private static IRuleProviderFactory? CreateFactoryForKind(
            string canonicalPath,
            RuleFileKind kind,
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfiguration,
            IAnalysisLogger? logger)
        {
            switch (kind)
            {
                case RuleFileKind.Assembly:
                    return CreateAssemblyFactory(canonicalPath, ruleConfiguration, logger);

                case RuleFileKind.ScriptModule:
                case RuleFileKind.ModuleManifest:
                    return new PSModuleRuleProviderFactory(canonicalPath, logger);

                default:
                    logger?.Warning($"Unsupported rule file kind for '{canonicalPath}'.");
                    return null;
            }
        }

        private static IRuleProviderFactory? CreateAssemblyFactory(
            string canonicalPath,
            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfiguration,
            IAnalysisLogger? logger)
        {
            try
            {
                Assembly assembly;
#if CORECLR
                // On .NET Core/5+, load into an isolated collectible ALC so dependency
                // conflicts between rule assemblies and the host are avoided.
                string assemblyDir = Path.GetDirectoryName(canonicalPath) ?? ".";
                var alc = new RuleAssemblyLoadContext(assemblyDir);
                assembly = alc.LoadFromAssemblyPath(canonicalPath);
#else
                // AssemblyLoadContext does not exist on .NET Framework 4.6.2 (Windows PowerShell).
                // Assembly loads into the default AppDomain. Path validation and ownership checks
                // are still enforced; the ALC isolation is the only missing layer.
                assembly = Assembly.LoadFile(canonicalPath);
#endif
                logger?.Debug($"Loaded external rule assembly: {assembly.GetName().Name} from '{canonicalPath}'.");
                return TypeRuleProviderFactory.FromAssembly(ruleConfiguration, assembly);
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to load assembly '{canonicalPath}': {ex.Message}");
                return null;
            }
        }

        private static string ResolvePath(string rawPath, string? settingsFileDirectory)
        {
            if (Path.IsPathRooted(rawPath))
            {
                return rawPath;
            }

            if (settingsFileDirectory is not null)
            {
                return Path.Combine(settingsFileDirectory, rawPath);
            }

            return Path.GetFullPath(rawPath);
        }
    }
}
