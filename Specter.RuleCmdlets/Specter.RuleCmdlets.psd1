@{
    RootModule = if ($PSEdition -eq 'Core')
    {
        'net8/Specter.RuleCmdlets.dll'
    }
    else
    {
        'net462/Specter.RuleCmdlets.dll'
    }
    ModuleVersion = '0.1.0'
    GUID = '3e9c4e7a-8f2d-4b6c-a1d0-5f3e8c7b9a02'
    Author = 'Robert Holt'
    Description = 'Rule-authoring cmdlets for Specter analysis rules.'
    PowerShellVersion = '5.1'
    DotNetFrameworkVersion = '4.6.2'
    FunctionsToExport = @()
    CmdletsToExport = @(
        'Write-Diagnostic'
        'New-ScriptCorrection'
    )
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Prerelease = 'preview.1'
        }
    }
    CompatiblePSEditions = @('Core', 'Desktop')
}
