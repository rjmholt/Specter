using PSpecter.Builder;
using PSpecter.Execution;
using PSpecter.Instantiation;
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

    public static AnalysisService CreateDefault()
    {
        ScriptAnalyzer analyzer = new ScriptAnalyzerBuilder()
            .WithRuleComponentProvider(b => b.UseBuiltinDatabase())
            .WithRuleExecutorFactory(new ParallelLinqRuleExecutorFactory())
            .AddBuiltinRules()
            .Build();

        return new AnalysisService(analyzer);
    }

    public IReadOnlyCollection<ScriptDiagnostic> AnalyzeScriptContent(string content, string? filePath = null)
    {
        if (filePath is not null)
        {
            return _analyzer.AnalyzeScriptInput(content);
        }

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
