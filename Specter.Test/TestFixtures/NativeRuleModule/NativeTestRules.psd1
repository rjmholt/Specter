@{
    RootModule        = 'NativeTestRules.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = 'c3d4e5f6-a7b8-9012-cdef-345678901234'
    Author            = 'Specter Test'
    Description       = 'Example rule module using the native [SpecterRule] convention.'
    FunctionsToExport = @(
        'Test-NoWriteHost'
        'Test-NoHardcodedPasswords'
    )
}
