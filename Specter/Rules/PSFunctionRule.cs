using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Threading;
using Specter.Instantiation;
using Specter.Logging;

namespace Specter.Rules
{
    /// <summary>
    /// A rule implemented as a PowerShell function running in a restricted runspace.
    /// Wraps function invocations with per-call timeout, output sanitisation, and
    /// consecutive-failure auto-disable logic.
    /// </summary>
    internal sealed class PSFunctionRule : ScriptRule, IDisposable
    {
        private const int DefaultTimeoutMs = 30_000;
        private const int MaxConsecutiveTimeouts = 3;
        private const long MemoryWarningThresholdBytes = 100L * 1024 * 1024;

        private readonly Runspace _runspace;
        private readonly string _functionName;
        private readonly PSRuleConvention _convention;
        private readonly IAnalysisLogger? _logger;
        private readonly int _timeoutMs;

        private int _consecutiveTimeouts;
        private bool _disabled;

        internal PSFunctionRule(
            RuleInfo ruleInfo,
            Runspace runspace,
            string functionName,
            PSRuleConvention convention,
            IAnalysisLogger? logger,
            int timeoutMs = DefaultTimeoutMs)
            : base(ruleInfo)
        {
            _runspace = runspace;
            _functionName = functionName;
            _convention = convention;
            _logger = logger;
            _timeoutMs = timeoutMs;
        }

        public override IEnumerable<ScriptDiagnostic> AnalyzeScript(
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? scriptPath)
        {
            if (_disabled)
            {
                yield break;
            }

            long memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            List<ScriptDiagnostic>? results = null;

            try
            {
                results = InvokeWithTimeout(ast, tokens, scriptPath);
            }
            catch (OperationCanceledException)
            {
                HandleTimeout();
                yield break;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Rule '{RuleInfo.FullName}' threw an exception: {ex.Message}");
                yield break;
            }

            Interlocked.Exchange(ref _consecutiveTimeouts, 0);

            if (results is not null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    yield return results[i];
                }
            }

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
            long delta = memoryAfter - memoryBefore;
            if (delta > MemoryWarningThresholdBytes)
            {
                _logger?.Warning(
                    $"Rule '{RuleInfo.FullName}' increased heap by {delta / (1024 * 1024)} MB in a single invocation.");
            }
        }

        private List<ScriptDiagnostic>? InvokeWithTimeout(
            Ast ast,
            IReadOnlyList<Token> tokens,
            string? scriptPath)
        {
            using var ps = PowerShell.Create();
            ps.Runspace = _runspace;

            ps.AddCommand(_functionName);

            if (_convention == PSRuleConvention.PssaLegacy)
            {
                // PSSA legacy rules take a ScriptBlockAst as their first positional parameter.
                // Bind positionally to support both standard ($ScriptBlockAst) and
                // non-standard parameter names (e.g. $testAst in PSSA sample rules).
                ps.AddArgument(ast);
            }
            else
            {
                ps.AddParameter("Ast", ast)
                    .AddParameter("Tokens", tokens)
                    .AddParameter("ScriptPath", scriptPath);
            }

            IAsyncResult asyncResult = ps.BeginInvoke();

            if (!asyncResult.AsyncWaitHandle.WaitOne(_timeoutMs))
            {
                ps.Stop();
                throw new OperationCanceledException($"Rule '{RuleInfo.FullName}' timed out after {_timeoutMs}ms.");
            }

            var output = ps.EndInvoke(asyncResult);

            if (ps.HadErrors)
            {
                foreach (var error in ps.Streams.Error)
                {
                    _logger?.Warning($"Rule '{RuleInfo.FullName}' reported error: {error}");
                }
            }

            return SanitizeOutput(output, ast);
        }

        private List<ScriptDiagnostic>? SanitizeOutput(
            PSDataCollection<PSObject> output,
            Ast scriptAst)
        {
            if (output.Count == 0)
            {
                return null;
            }

            var results = new List<ScriptDiagnostic>(output.Count);
            IScriptExtent scriptExtent = scriptAst.Extent;

            foreach (PSObject pso in output)
            {
                if (pso.BaseObject is ScriptDiagnostic diagnostic)
                {
                    if (IsExtentWithinScript(diagnostic.ScriptExtent, scriptExtent))
                    {
                        results.Add(diagnostic);
                    }
                    else
                    {
                        _logger?.Warning(
                            $"Rule '{RuleInfo.FullName}' returned a diagnostic with extent outside the analysed script. Discarded.");
                    }

                    continue;
                }

                // PSSA legacy rules return DiagnosticRecord objects or hashtables
                ScriptDiagnostic? mapped = TryMapFromPsObject(pso);
                if (mapped is not null)
                {
                    results.Add(mapped);
                    continue;
                }

                _logger?.Warning(
                    $"Rule '{RuleInfo.FullName}' returned an object of type '{pso.BaseObject.GetType().FullName}' " +
                    "which is not a supported diagnostic type. Discarded.");
            }

            return results;
        }

        /// <summary>
        /// Tries to extract a diagnostic from a PSObject, handling:
        ///   - Hashtable with Message/Extent/Severity keys (PSSA convention)
        ///   - Any object with Message and Extent properties (covers DiagnosticRecord
        ///     and similar types without requiring a compile-time reference)
        /// </summary>
        private ScriptDiagnostic? TryMapFromPsObject(PSObject pso)
        {
            if (pso.BaseObject is Hashtable ht)
            {
                return TryMapFromHashtable(ht);
            }

            return TryMapFromProperties(pso);
        }

        private ScriptDiagnostic? TryMapFromHashtable(Hashtable ht)
        {
            string? message = ht["Message"]?.ToString();
            if (message is null)
            {
                return null;
            }

            IScriptExtent? extent = ht["Extent"] as IScriptExtent;
            if (extent is null)
            {
                return null;
            }

            DiagnosticSeverity severity = ParseSeverity(ht["Severity"]);

            return new ScriptDiagnostic(RuleInfo, message, extent, severity);
        }

        private ScriptDiagnostic? TryMapFromProperties(PSObject pso)
        {
            PSPropertyInfo? messageProp = pso.Properties["Message"];
            PSPropertyInfo? extentProp = pso.Properties["Extent"];

            if (messageProp is null || extentProp is null)
            {
                return null;
            }

            string? message = messageProp.Value?.ToString();
            if (message is null)
            {
                return null;
            }

            IScriptExtent? extent = extentProp.Value as IScriptExtent;
            if (extent is null)
            {
                return null;
            }

            PSPropertyInfo? severityProp = pso.Properties["Severity"];
            DiagnosticSeverity severity = ParseSeverity(severityProp?.Value);

            return new ScriptDiagnostic(RuleInfo, message, extent, severity);
        }

        private static DiagnosticSeverity ParseSeverity(object? value)
        {
            if (value is DiagnosticSeverity ds)
            {
                return ds;
            }

            if (value is string severityStr
                && Enum.TryParse<DiagnosticSeverity>(severityStr, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // PSSA's DiagnosticSeverity enum values match ours by name
            if (value is Enum e
                && Enum.TryParse<DiagnosticSeverity>(e.ToString(), ignoreCase: true, out var fromEnum))
            {
                return fromEnum;
            }

            return DiagnosticSeverity.Warning;
        }

        private static bool IsExtentWithinScript(IScriptExtent diagnosticExtent, IScriptExtent scriptExtent)
        {
            if (diagnosticExtent.StartLineNumber < scriptExtent.StartLineNumber)
            {
                return false;
            }

            if (diagnosticExtent.EndLineNumber > scriptExtent.EndLineNumber)
            {
                return false;
            }

            return true;
        }

        private void HandleTimeout()
        {
            int count = Interlocked.Increment(ref _consecutiveTimeouts);
            _logger?.Warning($"Rule '{RuleInfo.FullName}' timed out ({count}/{MaxConsecutiveTimeouts}).");

            if (count >= MaxConsecutiveTimeouts)
            {
                _disabled = true;
                _logger?.Warning($"Rule '{RuleInfo.FullName}' disabled after {MaxConsecutiveTimeouts} consecutive timeouts.");
            }
        }

        public void Dispose()
        {
            _runspace?.Dispose();
        }
    }
}
