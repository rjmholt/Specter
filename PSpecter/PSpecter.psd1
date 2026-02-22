#
# Module manifest for module 'PSpecter'
#

@{

# Author of this module
Author = 'PSpecter Contributors'

# Script module or binary module file associated with this manifest.
RootModule = if ($PSEdition -eq 'Core')
    {
        'net8/PSpecter.dll'
    }
    else
    {
        'net462/PSpecter.dll'
    }

# Version number of this module.
ModuleVersion = '2.0.0'

# ID used to uniquely identify this module
GUID = 'd6245802-193d-4068-a631-8863a4342a18'

# Company or vendor of this module
CompanyName = 'PSpecter'

# Copyright statement for this module
Copyright = '(c) PSpecter Contributors'

# Description of the functionality provided by this module
Description = 'PSpecter is a static analyzer and formatter for PowerShell, checking for potential code defects in scripts by applying a group of built-in or customized rules.'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '5.1'

# Minimum version of Microsoft .NET Framework required by this module
DotNetFrameworkVersion = '4.6.2'

# Type files (.ps1xml) to be loaded when importing this module
TypesToProcess = @()

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @()

# Functions to export from this module
FunctionsToExport = @()

# Cmdlets to export from this module
CmdletsToExport = @('Invoke-ScriptAnalyzer2', 'Write-Diagnostic')

# Variables to export from this module
VariablesToExport = @()

# Aliases to export from this module
AliasesToExport = @()

# Private data to pass to the module specified in RootModule/ModuleToProcess
PrivateData = @{
    PSData = @{
        Tags = 'lint', 'bestpractice', 'analyzer'
        ReleaseNotes = ''
        Prerelease = 'preview.1'
    }
}

CompatiblePSEditions = @('Core', 'Desktop')

}
