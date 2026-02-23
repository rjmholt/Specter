using Specter;
using Specter.Rules;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace Specter.RulePrimitives.Commands
{
    /// <summary>
    /// Emits a ScriptDiagnostic, automatically inferring the current rule from the call stack.
    /// This is the primary cmdlet for reporting findings from a rule function.
    ///
    /// Two parameter sets control how corrections are specified:
    ///   InlineCorrection: -CorrectionText [-CorrectionDescription] for simple same-extent fixes.
    ///   ExplicitCorrection: -Correction for pre-built Correction objects (different extent or multiple fixes).
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "Diagnostic", DefaultParameterSetName = InlineCorrectionSet)]
    [OutputType(typeof(ScriptDiagnostic))]
    public class WriteDiagnosticCommand : PSCmdlet
    {
        private const string InlineCorrectionSet = "InlineCorrection";
        private const string ExplicitCorrectionSet = "ExplicitCorrection";

        private RuleInfo? _rule;
        private bool _severityExplicitlySet;

        [Parameter(Mandatory = true, Position = 0)]
        public string Message { get; set; } = null!;

        [Parameter(Mandatory = true, Position = 1)]
        public IScriptExtent Extent { get; set; } = null!;

        [Parameter]
        public DiagnosticSeverity Severity
        {
            get => _severity;
            set
            {
                _severity = value;
                _severityExplicitlySet = true;
            }
        }
        private DiagnosticSeverity _severity = DiagnosticSeverity.Warning;

        /// <summary>
        /// Replacement text for a single correction at the diagnostic extent.
        /// For the common case where the fix replaces exactly the flagged span.
        /// </summary>
        [Parameter(ParameterSetName = InlineCorrectionSet)]
        public string? CorrectionText { get; set; }

        /// <summary>
        /// Description of the inline correction (e.g. "Replace alias with full name").
        /// Only used with -CorrectionText.
        /// </summary>
        [Parameter(ParameterSetName = InlineCorrectionSet)]
        public string? CorrectionDescription { get; set; }

        /// <summary>
        /// Pre-built Correction objects for advanced scenarios: multiple corrections,
        /// or corrections at extents different from the diagnostic.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ExplicitCorrectionSet)]
        public Correction[] Correction { get; set; } = null!;

        protected override void BeginProcessing()
        {
            _rule = GetRule();
        }

        protected override void ProcessRecord()
        {
            IReadOnlyList<Correction>? correction = BuildCorrection();

            DiagnosticSeverity severity = !_severityExplicitlySet && _rule is not null
                ? _rule.DefaultSeverity
                : _severity;

            WriteObject(new ScriptDiagnostic(_rule, Message, Extent, severity, correction));
        }

        private IReadOnlyList<Correction>? BuildCorrection()
        {
            if (CorrectionText is not null)
            {
                return new[] { new Correction(Extent, CorrectionText, CorrectionDescription ?? string.Empty) };
            }

            if (Correction is { Length: > 0 })
            {
                return Correction;
            }

            return null;
        }

        private RuleInfo? GetRule()
        {
            Debugger debugger = Runspace.DefaultRunspace.Debugger;

            foreach (CallStackFrame frame in debugger.GetCallStack())
            {
                if (frame.InvocationInfo.MyCommand is not FunctionInfo function)
                {
                    continue;
                }

                if (RuleInfo.TryGetFromFunctionInfo(function, out RuleInfo? ruleInfo))
                {
                    return ruleInfo;
                }
            }

            return null;
        }
    }
}
