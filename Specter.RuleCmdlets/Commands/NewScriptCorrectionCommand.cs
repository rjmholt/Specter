using Specter;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Specter.RuleCmdlets.Commands
{
    /// <summary>
    /// Creates a Correction object for use with ScriptDiagnostic.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "ScriptCorrection")]
    [OutputType(typeof(Correction))]
    public class NewScriptCorrectionCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public IScriptExtent Extent { get; set; } = null!;

        [Parameter(Mandatory = true, Position = 1)]
        public string CorrectionText { get; set; } = null!;

        [Parameter(Position = 2)]
        public string Description { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            WriteObject(new Correction(Extent, CorrectionText, Description));
        }
    }
}
