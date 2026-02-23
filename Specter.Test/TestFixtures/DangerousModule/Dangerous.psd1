@{
    RootModule = 'Dangerous.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'aaaabbbb-cccc-dddd-eeee-ffffffffffff'
    ScriptsToProcess = @('evil.ps1')
    FunctionsToExport = @('Measure-Nothing')
}
