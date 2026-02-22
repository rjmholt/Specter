using PSpecter.Logging;
using System;
using System.Management.Automation;

namespace PSpecter.PssaCompatibility
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
                "PSpecterAnalysisError",
                ErrorCategory.NotSpecified,
                targetObject: null));
        }

        public void Error(string message, Exception exception)
        {
            _cmdlet.WriteError(new ErrorRecord(
                exception,
                "PSpecterAnalysisError",
                ErrorCategory.NotSpecified,
                targetObject: null));
        }
    }
}
