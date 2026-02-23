#Requires -Version 3.0

if ([System.Threading.Thread]::CurrentThread.CurrentUICulture.Name -ne 'en-US') {
    Import-LocalizedData -BindingVariable Messages -UICulture 'en-US'
} else {
    Import-LocalizedData -BindingVariable Messages
}

<#
.DESCRIPTION
    Detects use of Write-Host, following the CommunityAnalyzerRules pattern:
    Measure-* with Import-LocalizedData and DiagnosticRecord output.
#>
function Measure-WriteHost {
    [CmdletBinding()]
    [OutputType([Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord[]])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.ScriptBlockAst]
        $ScriptBlockAst
    )

    $predicate = {
        param([System.Management.Automation.Language.Ast]$Ast)
        $commandAst = $Ast -as [System.Management.Automation.Language.CommandAst]
        if ($commandAst) {
            return ($commandAst.CommandElements[0].Extent.Text -eq 'Write-Host')
        }
        return $false
    }

    $foundNode = $ScriptBlockAst.Find($predicate, $false)
    if ($foundNode) {
        $result = New-Object `
            -TypeName 'Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord' `
            -ArgumentList @(
                $Messages.MeasureWriteHost,
                $foundNode.Extent,
                $PSCmdlet.MyInvocation.InvocationName,
                'Warning',
                $null
            )
        return $result
    }
}
