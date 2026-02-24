using Specter;
using Specter.Builder;
using Specter.Configuration;
using Specter.Configuration.Json;
using Specter.Configuration.Psd;
using Specter.Execution;
using Specter.Formatting;
using Specter.Instantiation;
using Specter.Logging;
using Specter.Rules.Builtin;
using Specter.Rules;
using System.Collections.Concurrent;

namespace Specter.Server;

/// <summary>
/// Shared analysis service used by both LSP and gRPC endpoints.
/// Manages a ScriptAnalyzer instance and provides thread-safe analysis.
/// Hot-reloads the analyzer when the configuration file changes on disk.
/// </summary>
public sealed class AnalysisService : IDisposable
{
    private volatile ScriptAnalyzer _analyzer;
    private readonly ConcurrentDictionary<string, DocumentState> _openDocuments = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAnalysisLogger _logger;
    private readonly string? _configPath;
    private FileSystemWatcher? _configWatcher;
    private Timer? _debounceTimer;
    private int _reloading;

    private static readonly TimeSpan s_debounceDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Raised after the configuration has been successfully reloaded.
    /// </summary>
    public event EventHandler? ConfigurationReloaded;

    public AnalysisService(ScriptAnalyzer analyzer, IAnalysisLogger? logger = null, string? configPath = null)
    {
        _analyzer = analyzer;
        _logger = logger ?? ConsoleAnalysisLogger.Instance;
        _configPath = configPath;

        if (_configPath is not null)
        {
            StartConfigWatcher(_configPath);
        }
    }

    public static AnalysisService CreateDefault(IAnalysisLogger? logger = null)
    {
        var resolvedLogger = logger ?? ConsoleAnalysisLogger.Instance;
        ScriptAnalyzer analyzer = BuildAnalyzer(ruleConfiguration: null, resolvedLogger);
        return new AnalysisService(analyzer, resolvedLogger);
    }

    public static AnalysisService CreateFromConfig(string? configPath, IAnalysisLogger? logger = null)
    {
        if (string.IsNullOrEmpty(configPath))
        {
            return CreateDefault(logger);
        }

        var resolvedLogger = logger ?? ConsoleAnalysisLogger.Instance;
        string fullPath = Path.GetFullPath(configPath);

        if (!File.Exists(fullPath))
        {
            resolvedLogger.Warning($"Configuration file not found: {fullPath}. Using default settings.");
            return CreateDefault(resolvedLogger);
        }

        IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfig = LoadRuleConfiguration(fullPath, resolvedLogger);
        ScriptAnalyzer analyzer = BuildAnalyzer(ruleConfig, resolvedLogger);
        resolvedLogger.Verbose($"Loaded configuration from: {fullPath}");

        return new AnalysisService(analyzer, resolvedLogger, fullPath);
    }

    public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptContent(string content, string? filePath = null)
    {
        return _analyzer.AnalyzeScriptInput(content);
    }

    public IReadOnlyCollection<ScriptDiagnostic> AnalyzeFile(string filePath)
    {
        return _analyzer.AnalyzeScriptPath(filePath);
    }

    public IReadOnlyList<RuleInfo> GetRules()
    {
        ScriptAnalyzer analyzer = _analyzer;
        var rules = new List<RuleInfo>();
        foreach (IRuleProvider provider in analyzer.RuleProviders)
        {
            foreach (ScriptRule rule in provider.GetScriptRules())
            {
                rules.Add(rule.RuleInfo);
            }
        }
        return rules;
    }

    public FormatResult FormatScript(string content, string? filePath, string? presetName)
    {
        IReadOnlyDictionary<string, IEditorConfiguration> configs = ResolveFormatterConfigs(presetName);
        ScriptFormatter formatter = ScriptFormatter.FromEditorConfigs(configs);
        string formatted = formatter.Format(content, filePath);
        return new FormatResult(formatted, !string.Equals(content, formatted, StringComparison.Ordinal));
    }

    public void OpenDocument(string uri, string content, int version)
    {
        _openDocuments[uri] = new DocumentState(content, version);
    }

    public void UpdateDocument(string uri, string content, int version)
    {
        _openDocuments[uri] = new DocumentState(content, version);
    }

    public void CloseDocument(string uri)
    {
        _openDocuments.TryRemove(uri, out _);
    }

    public DocumentState? GetDocument(string uri)
    {
        return _openDocuments.TryGetValue(uri, out DocumentState? state) ? state : null;
    }

    public void Dispose()
    {
        _configWatcher?.Dispose();
        _debounceTimer?.Dispose();
    }

    private static ScriptAnalyzer BuildAnalyzer(
        IReadOnlyDictionary<string, IRuleConfiguration?>? ruleConfiguration,
        IAnalysisLogger logger)
    {
        var builder = new ScriptAnalyzerBuilder()
            .WithLogger(logger)
            .WithRuleComponentProvider(b => b.UseBuiltinDatabase())
            .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory(logger));

        if (ruleConfiguration is not null)
        {
            builder.AddBuiltinRules(ruleConfiguration);
        }
        else
        {
            builder.AddBuiltinRules();
        }

        return builder.Build();
    }

    private static IReadOnlyDictionary<string, IRuleConfiguration?> LoadRuleConfiguration(
        string fullPath,
        IAnalysisLogger logger)
    {
        string extension = Path.GetExtension(fullPath);

        try
        {
            IScriptAnalyzerConfiguration config = string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                ? JsonScriptAnalyzerConfiguration.FromFile(fullPath)
                : PsdScriptAnalyzerConfiguration.FromFile(fullPath);

            return config.RuleConfiguration;
        }
        catch (Exception ex)
        {
            logger.Warning($"Failed to load configuration from {fullPath}: {ex.Message}. Using default settings.");
            return new Dictionary<string, IRuleConfiguration?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void StartConfigWatcher(string configPath)
    {
        string? directory = Path.GetDirectoryName(configPath);
        string fileName = Path.GetFileName(configPath);

        if (directory is null)
        {
            return;
        }

        _configWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        _configWatcher.Changed += OnConfigFileChanged;
        _configWatcher.Created += OnConfigFileChanged;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(DebouncedReload, null, s_debounceDelay, Timeout.InfiniteTimeSpan);
    }

    private void DebouncedReload(object? state)
    {
        if (Interlocked.CompareExchange(ref _reloading, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (_configPath is null || !File.Exists(_configPath))
            {
                return;
            }

            _logger.Verbose($"Configuration file changed, reloading: {_configPath}");

            IReadOnlyDictionary<string, IRuleConfiguration?> ruleConfig = LoadRuleConfiguration(_configPath, _logger);
            ScriptAnalyzer newAnalyzer = BuildAnalyzer(ruleConfig, _logger);

            _analyzer = newAnalyzer;

            _logger.Verbose("Configuration reloaded successfully.");
            ConfigurationReloaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to reload configuration: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _reloading, 0);
        }
    }

    private static IReadOnlyDictionary<string, IEditorConfiguration> ResolveFormatterConfigs(string? presetName)
    {
        if (string.IsNullOrEmpty(presetName)
            || string.Equals(presetName, "Default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(presetName, "Stroustrup", StringComparison.OrdinalIgnoreCase))
        {
            return FormatterPresets.Default;
        }

        if (string.Equals(presetName, "OTBS", StringComparison.OrdinalIgnoreCase))
        {
            return FormatterPresets.OTBS;
        }

        if (string.Equals(presetName, "Allman", StringComparison.OrdinalIgnoreCase))
        {
            return FormatterPresets.Allman;
        }

        return FormatterPresets.Default;
    }
}

public sealed record DocumentState(string Content, int Version);

public sealed record FormatResult(string FormattedContent, bool Changed);
