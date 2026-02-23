#
# Module manifest for module 'Specter'
#

@{

# Author of this module
Author = 'Specter Contributors'

# Script module or binary module file associated with this manifest.
RootModule = if ($PSEdition -eq 'Core')
    {
        'net8/Specter.Module.dll'
    }
    else
    {
        'net462/Specter.Module.dll'
    }

# Version number of this module.
ModuleVersion = '0.1.0'

# ID used to uniquely identify this module
GUID = 'd6245802-193d-4068-a631-8863a4342a18'

# Company or vendor of this module
CompanyName = 'Specter'

# Copyright statement for this module
Copyright = '(c) Specter Contributors'

# Description of the functionality provided by this module
Description = 'Specter is a static analyzer and formatter for PowerShell, checking for potential code defects in scripts by applying a group of built-in or customized rules.'

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
CmdletsToExport = @('Invoke-Specter', 'Update-SpecterDatabase')

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
