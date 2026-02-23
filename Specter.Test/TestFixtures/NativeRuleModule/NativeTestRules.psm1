function Test-NoWriteHost {
    [SpecterRule('AvoidWriteHost', 'Write-Host should not be used in reusable scripts.')]
    param(
        [System.Management.Automation.Language.Ast]$Ast,
        [System.Management.Automation.Language.Token[]]$Tokens,
        [string]$ScriptPath
    )

    $commands = $Ast.FindAll({
        $args[0] -is [System.Management.Automation.Language.CommandAst]
    }, $true)

    foreach ($cmd in $commands) {
        if ($cmd.GetCommandName() -eq 'Write-Host') {
            # Uses New-ScriptCorrection because the fix targets the command name
            # element, not the full command extent flagged by the diagnostic.
            $fix = New-ScriptCorrection `
                -Extent $cmd.CommandElements[0].Extent `
                -CorrectionText 'Write-Output' `
                -Description 'Replace Write-Host with Write-Output'

            Write-Diagnostic `
                -Message "Avoid using Write-Host; use Write-Output or Write-Information instead." `
                -Extent $cmd.Extent `
                -Correction $fix
        }
    }
}

function Test-NoHardcodedPasswords {
    [SpecterRule(
        'AvoidHardcodedPasswords',
        'Detects assignment to variables named *password* with string literal values.',
        Severity = 'Error'
    )]
    param(
        [System.Management.Automation.Language.Ast]$Ast,
        [System.Management.Automation.Language.Token[]]$Tokens,
        [string]$ScriptPath
    )

    $assignments = $Ast.FindAll({
        $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst]
    }, $true)

    foreach ($assign in $assignments) {
        $target = $assign.Left
        if ($target -is [System.Management.Automation.Language.VariableExpressionAst]) {
            $varName = $target.VariablePath.UserPath
            if ($varName -match 'password|passwd|secret|apikey' -and
                $assign.Right -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                Write-Diagnostic `
                    -Message "Variable '`$$varName' appears to contain a hardcoded credential. Use a secret store instead." `
                    -Extent $assign.Extent
            }
        }
    }
}
