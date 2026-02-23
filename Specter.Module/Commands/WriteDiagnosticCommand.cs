using Specter.Rules;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace Specter.Module.Commands
{
    [Cmdlet(VerbsCommunications.Write, "Diagnostic")]
    [OutputType(typeof(ScriptDiagnostic))]
    public class WriteDiagnosticCommand : PSCmdlet
    {
        private RuleInfo? _rule;

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public IScriptExtent[] Extent { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string[] Message { get; set; } = null!;

        [Parameter]
        public DiagnosticSeverity? Severity { get; set; }

        protected override void BeginProcessing()
        {
            _rule = GetRule();
        }

        protected override void ProcessRecord()
        {
            for (int i = 0; i < Message.Length; i++)
            {
                WriteObject(new ScriptDiagnostic(_rule, Message[i], Extent[i], Severity ?? _rule?.DefaultSeverity ?? DiagnosticSeverity.Warning));
            }
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
