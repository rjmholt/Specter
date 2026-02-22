#
# Module manifest for module 'PSpecter.PssaCompatibility'
# Drop-in replacement for PSScriptAnalyzer, wrapping the PSpecter engine.
#

@{

# Script module or binary module file associated with this manifest.
RootModule = 'PSpecter.PssaCompatibility.dll'

# Version number of this module
ModuleVersion = '0.1.0'

# ID used to uniquely identify this module
GUID = 'b7e4e482-7c3f-4c96-a57b-2c3e11f8c1d5'

Author = 'PSpecter Contributors'

CompanyName = 'PSpecter'

Copyright = '(c) PSpecter Contributors. All rights reserved.'

Description = 'Drop-in compatibility module providing PSScriptAnalyzer-compatible Invoke-ScriptAnalyzer and Get-ScriptAnalyzerRule commands backed by the PSpecter engine.'

PowerShellVersion = '5.1'

DotNetFrameworkVersion = '4.6.2'

TypesToProcess = @()

FormatsToProcess = @()

FunctionsToExport = @()

CmdletsToExport = @('Invoke-ScriptAnalyzer', 'Get-ScriptAnalyzerRule', 'Invoke-Formatter')

VariablesToExport = @()

AliasesToExport = @()

CompatiblePSEditions = @('Core', 'Desktop')

PrivateData = @{
    PSData = @{
        Tags = @('lint', 'PSScriptAnalyzer', 'compatibility')
        Prerelease = 'alpha.1'
    }
}

}
