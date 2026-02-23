using Specter.Logging;
using System;
using System.Management.Automation;

namespace Specter.PssaCompatibility
{
    internal sealed class PowerShellAnalysisLogger : IAnalysisLogger
    {
        private readonly PSCmdlet _cmdlet;

        public PowerShellAnalysisLogger(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        public void Debug(string message) => _cmdlet.WriteDebug(message);

        public void Verbose(string message) => _cmdlet.WriteVerbose(message);

        public void Warning(string message) => _cmdlet.WriteWarning(message);

        public void Error(string message)
        {
            _cmdlet.WriteError(new ErrorRecord(
                new InvalidOperationException(message),
                "SpecterAnalysisError",
                ErrorCategory.NotSpecified,
                targetObject: null));
        }

        public void Error(string message, Exception exception)
        {
            _cmdlet.WriteError(new ErrorRecord(
                exception,
                "SpecterAnalysisError",
                ErrorCategory.NotSpecified,
                targetObject: null));
        }
    }
}
