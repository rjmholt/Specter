#Requires -Version 3.0

<#
.DESCRIPTION
    Detects use of Invoke-Expression. Follows the InjectionHunter pattern:
    Measure-* function returning DiagnosticRecord via New-Object.
#>
function Measure-InvokeExpression {
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
            $name = $commandAst.CommandElements[0].Extent.Text
            return ($name -eq 'Invoke-Expression' -or $name -eq 'iex')
        }
        return $false
    }

    $foundNode = $ScriptBlockAst.Find($predicate, $false)
    if ($foundNode) {
        $result = New-Object `
            -TypeName 'Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord' `
            -ArgumentList @(
                'Possible script injection risk via Invoke-Expression.',
                $foundNode.Extent,
                $PSCmdlet.MyInvocation.InvocationName,
                'Warning',
                $null
            )
        return $result
    }
}

<#
.DESCRIPTION
    Detects calls to dangerous .NET methods that can execute arbitrary code.
    Uses a non-standard parameter name ($testAst) to exercise positional binding.
#>
function Measure-DangerousMethod {
    [CmdletBinding()]
    [OutputType([Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord[]])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.ScriptBlockAst]
        $testAst
    )

    $predicate = {
        param([System.Management.Automation.Language.Ast]$Ast)
        $memberAst = $Ast -as [System.Management.Automation.Language.InvokeMemberExpressionAst]
        if ($memberAst) {
            $methodName = $memberAst.Member.Extent.Text
            return ($methodName -in @('InvokeScript', 'CreateNestedPipeline', 'AddScript', 'NewScriptBlock', 'ExpandString'))
        }
        return $false
    }

    $foundNode = $testAst.Find($predicate, $false)
    if ($foundNode) {
        $result = New-Object `
            -TypeName 'Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord' `
            -ArgumentList @(
                "Possible injection risk via dangerous method: $($foundNode.Member.Extent.Text).",
                $foundNode.Extent,
                $PSCmdlet.MyInvocation.InvocationName,
                'Warning',
                $null
            )
        return $result
    }
}
