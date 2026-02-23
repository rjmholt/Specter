using PSpecter.Builder;
using PSpecter.Builtin;
using PSpecter.Execution;
using PSpecter.Formatting;
using PSpecter.Instantiation;
using PSpecter.Logging;
using PSpecter.Rules;
using System.Collections.Concurrent;

namespace PSpecter.Server;

/// <summary>
/// Shared analysis service used by both LSP and gRPC endpoints.
/// Manages a ScriptAnalyzer instance and provides thread-safe analysis.
/// </summary>
public sealed class AnalysisService : IDisposable
{
    private readonly ScriptAnalyzer _analyzer;
    private readonly ConcurrentDictionary<string, DocumentState> _openDocuments = new(StringComparer.OrdinalIgnoreCase);

    public AnalysisService(ScriptAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public static AnalysisService CreateDefault(IAnalysisLogger? logger = null)
    {
        var resolvedLogger = logger ?? ConsoleAnalysisLogger.Instance;

        ScriptAnalyzer analyzer = new ScriptAnalyzerBuilder()
            .WithLogger(resolvedLogger)
            .WithRuleComponentProvider(b => b.UseBuiltinDatabase())
            .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory(resolvedLogger))
            .AddBuiltinRules()
            .Build();

        return new AnalysisService(analyzer);
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
        var rules = new List<RuleInfo>();
        foreach (IRuleProvider provider in _analyzer.RuleProviders)
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
    }
}

public sealed record DocumentState(string Content, int Version);

public sealed record FormatResult(string FormattedContent, bool Changed);
