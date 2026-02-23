function Measure-EmptyDescription {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [System.Management.Automation.Language.ScriptBlockAst]$ScriptBlockAst
    )

    foreach ($function in $ScriptBlockAst.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
        $help = $function.GetHelpContent()
        if ($null -eq $help -or [string]::IsNullOrWhiteSpace($help.Synopsis)) {
            @{
                Message  = "Function '$($function.Name)' is missing a description."
                Extent   = $function.Extent
                RuleName = 'EmptyDescription'
                Severity = 'Warning'
            }
        }
    }
}
