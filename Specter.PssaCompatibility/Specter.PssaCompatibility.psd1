#
# Module manifest for module 'Specter.PssaCompatibility'
# Drop-in replacement for PSScriptAnalyzer, wrapping the Specter engine.
#

@{

# Script module or binary module file associated with this manifest.
RootModule = if ($PSEdition -eq 'Core')
    {
        'net8/Specter.PssaCompatibility.dll'
    }
    else
    {
        'net462/Specter.PssaCompatibility.dll'
    }

# Version number of this module
ModuleVersion = '0.1.0'

# ID used to uniquely identify this module
GUID = 'b7e4e482-7c3f-4c96-a57b-2c3e11f8c1d5'

Author = 'Specter Contributors'

CompanyName = 'Specter'

Copyright = '(c) Specter Contributors. All rights reserved.'

Description = 'Drop-in compatibility module providing PSScriptAnalyzer-compatible Invoke-ScriptAnalyzer and Get-ScriptAnalyzerRule commands backed by the Specter engine.'

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
        Prerelease = 'preview.1'
    }
}

}
