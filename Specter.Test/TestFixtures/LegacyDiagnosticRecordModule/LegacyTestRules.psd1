@{
    RootModule = 'LegacyTestRules.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'a069871c-e591-4dce-a1e9-b488c3cb26b4'
    Author = 'Specter Test'
    Description = 'Legacy PSSA-style rules that return DiagnosticRecord objects via New-Object.'
    FunctionsToExport = @(
        'Measure-InvokeExpression'
        'Measure-DangerousMethod'
    )
}
