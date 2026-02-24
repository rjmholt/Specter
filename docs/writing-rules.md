# Writing Custom Rules

Specter supports custom rules written in PowerShell and loaded via `-CustomRulePath` or the `RulePaths` configuration key. This guide walks through creating a rule module from scratch.

## Writing Rules in C#

Specter also supports custom rules compiled into a .NET class library (`.dll`).

### Project setup

Create a class library and reference the `Specter.Api` NuGet package:

```bash
dotnet new classlib -n MySpecterRules
cd MySpecterRules
dotnet add package Specter.Api
```

Target frameworks should match your compatibility goals. `net8.0` is the simplest starting point.

### Minimal rule example

```csharp
using Specter;
using Specter.Rules;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace MySpecterRules;

[Rule("SampleTestRule", "Flags all Invoke-Expression calls")]
public sealed class SampleTestRule : ScriptRule
{
    public SampleTestRule(RuleInfo ruleInfo) : base(ruleInfo) { }

    public override IEnumerable<ScriptDiagnostic> AnalyzeScript(Ast ast, IReadOnlyList<Token> tokens, string? scriptPath)
    {
        foreach (Ast node in ast.FindAll(
            static n => n is CommandAst cmd && cmd.GetCommandName() == "Invoke-Expression",
            searchNestedScriptBlocks: true))
        {
            yield return CreateDiagnostic("Avoid using Invoke-Expression.", node.Extent);
        }
    }
}
```

### Optional attributes and base types

- `[RuleCollection(Name = "...")]` on the assembly sets a default namespace for contained rules.
- `ConfigurableScriptRule<TConfiguration>` supports strongly-typed rule configuration.
- `FormattingRule<TConfiguration>` supports editor-backed formatting diagnostics.

### Local testing

Build the library and pass it as a custom rule path:

```powershell
dotnet build ./MySpecterRules.csproj
Invoke-Specter -Path ./script.ps1 -CustomRulePath ./bin/Debug/net8.0/MySpecterRules.dll
```

On .NET (Core/5+/6+/7+/8+), external assemblies are loaded with an isolated `AssemblyLoadContext` to reduce dependency conflicts. On .NET Framework (`net462`), this isolation model is not available and assembly loading uses the default AppDomain.

## Module Structure

A rule module is a standard PowerShell module. At minimum you need a `.psm1` file and a `.psd1` manifest:

```
MyRules/
  MyRules.psd1
  MyRules.psm1
```

## Rule Conventions

Specter discovers rules using two conventions:

### Native convention (recommended)

Decorate your function with the `[SpecterRule]` attribute. The function receives `$Ast`, `$Tokens`, and `$ScriptPath` parameters and emits diagnostics with `Write-Diagnostic`:

```powershell
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
            Write-Diagnostic "Avoid using Write-Host." -Extent $cmd.Extent
        }
    }
}
```

The `[SpecterRule]` attribute accepts these arguments:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | Rule name (used in configuration and suppression) |
| `description` | `string` | Yes | Human-readable description |
| `Severity` | `DiagnosticSeverity` | No | `Error`, `Warning` (default), or `Information` |
| `Namespace` | `string` | No | Rule namespace (defaults to the module name) |

### PSSA legacy convention

Functions named `Measure-*` with a `[ScriptBlockAst]` parameter are discovered automatically. This ensures compatibility with existing PSScriptAnalyzer custom rule modules:

```powershell
function Measure-EmptyDescription {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [System.Management.Automation.Language.ScriptBlockAst]$ScriptBlockAst
    )

    foreach ($function in $ScriptBlockAst.FindAll({
        $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst]
    }, $true)) {
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
```

## Emitting Diagnostics

### `Write-Diagnostic`

The primary cmdlet for reporting findings. It auto-detects the calling rule from the call stack, so the emitted diagnostic carries the correct rule name, severity, and metadata automatically.

**Basic usage:**

```powershell
Write-Diagnostic "Avoid using Write-Host." -Extent $cmd.Extent
```

**With an inline correction** -- use `-CorrectionText` to propose a replacement for the flagged span:

```powershell
Write-Diagnostic "Avoid using Write-Host." -Extent $cmd.Extent `
    -CorrectionText 'Write-Output' `
    -CorrectionDescription 'Replace Write-Host with Write-Output'
```

This creates a correction that replaces the diagnostic's extent with the given text. `-CorrectionDescription` is an optional explanation shown to the user.

**Overriding severity** -- if the `[SpecterRule]` attribute specifies a default severity, `Write-Diagnostic` uses it. Pass `-Severity` to override per-diagnostic:

```powershell
Write-Diagnostic "Critical problem" -Extent $node.Extent -Severity Error
```

**Parameter sets:**

`Write-Diagnostic` has two parameter sets for corrections. `-Message`, `-Extent`, and `-Severity` are common to both.

*InlineCorrection* (default) -- for simple same-extent fixes:

| Parameter | Type | Required | Default |
|-----------|------|----------|---------|
| `-CorrectionText` | `string` | No | `$null` |
| `-CorrectionDescription` | `string` | No | `$null` |

*ExplicitCorrection* -- for pre-built `Correction` objects (see `New-ScriptCorrection` below):

| Parameter | Type | Required | Default |
|-----------|------|----------|---------|
| `-Correction` | `Correction[]` | Yes | -- |

### `New-ScriptCorrection`

Creates a `Correction` object for use with `Write-Diagnostic -Correction`. You only need this when `-CorrectionText` is not enough. There are two reasons to reach for it:

- **The correction targets a different extent than the diagnostic** -- e.g. you want to flag a whole command but only replace the command name:

```powershell
$fix = New-ScriptCorrection `
    -Extent $cmd.CommandElements[0].Extent `
    -CorrectionText 'Write-Output'

Write-Diagnostic "Avoid Write-Host." -Extent $cmd.Extent -Correction $fix
```

- **Multiple corrections for a single diagnostic** -- e.g. a rule that renames a variable at its declaration and all usage sites:

```powershell
$fix = @(
    New-ScriptCorrection -Extent $declaration.Extent -CorrectionText '$newName'
    New-ScriptCorrection -Extent $usage1.Extent -CorrectionText '$newName'
    New-ScriptCorrection -Extent $usage2.Extent -CorrectionText '$newName'
)

Write-Diagnostic "Variable should be renamed." -Extent $declaration.Extent -Correction $fix
```

| Parameter | Type | Required | Default |
|-----------|------|----------|---------|
| `-Extent` | `IScriptExtent` | Yes (position 0) | -- |
| `-CorrectionText` | `string` | Yes (position 1) | -- |
| `-Description` | `string` | No (position 2) | `""` |

## Module Manifest

The manifest must export the rule functions. Keep it minimal:

```powershell
# MyRules.psd1
@{
    RootModule        = 'MyRules.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
    FunctionsToExport = @(
        'Test-NoWriteHost'
        'Test-NoHardcodedCredentials'
    )
}
```

Specter audits manifests before loading. The following fields cause the module to be rejected:

- `ScriptsToProcess` -- executes scripts during import
- `RequiredAssemblies` -- loads arbitrary assemblies
- `TypesToProcess` -- loads type extensions
- `FormatsToProcess` -- loads format files
- `NestedModules` referencing paths outside the module directory

## Complete Example

Here is a full example module with two rules -- one that detects `Write-Host` usage (with an auto-fix) and one that flags hardcoded credentials:

### `SecurityRules.psd1`

```powershell
@{
    RootModule        = 'SecurityRules.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = 'a2b3c4d5-e6f7-8901-abcd-ef2345678901'
    Author            = 'Your Name'
    Description       = 'Example Specter rule module.'
    FunctionsToExport = @(
        'Test-NoWriteHost'
        'Test-NoHardcodedCredentials'
    )
}
```

### `SecurityRules.psm1`

```powershell
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
            # Flag the whole command; fix only the command name element
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

function Test-NoHardcodedCredentials {
    [SpecterRule(
        'AvoidHardcodedCredentials',
        'Detects string literals that look like passwords or API keys.',
        Severity = 'Error'
    )]
    param(
        [System.Management.Automation.Language.Ast]$Ast,
        [System.Management.Automation.Language.Token[]]$Tokens,
        [string]$ScriptPath
    )

    $suspiciousPatterns = @(
        'password\s*=\s*[''"]',
        'api[-_]?key\s*=\s*[''"]',
        'secret\s*=\s*[''"]',
        'connectionstring\s*=\s*[''"].*password'
    )

    $strings = $Ast.FindAll({
        $args[0] -is [System.Management.Automation.Language.StringConstantExpressionAst]
    }, $true)

    foreach ($str in $strings) {
        foreach ($pattern in $suspiciousPatterns) {
            if ($str.Parent.Extent.Text -match $pattern) {
                Write-Diagnostic `
                    -Message "Possible hardcoded credential detected. Use a secret store or parameter instead." `
                    -Extent $str.Extent
                break
            }
        }
    }
}
```

## Loading Custom Rules

```powershell
# Via Invoke-Specter
Invoke-Specter -Path ./MyScript.ps1 -CustomRulePath ./SecurityRules/

# Via Invoke-ScriptAnalyzer (compatibility module)
Invoke-ScriptAnalyzer -Path ./MyScript.ps1 -CustomRulePath ./SecurityRules/

# Recursively load all rule modules in a directory
Invoke-Specter -Path ./MyScript.ps1 -CustomRulePath ./AllRules/ -RecurseCustomRulePath
```

Or in a JSON settings file:

```json
{
    "RulePaths": [
        "./SecurityRules/"
    ]
}
```

### Legacy PSScriptAnalyzer rule modules

Specter supports existing PSScriptAnalyzer custom rule modules that follow the `Measure-*` convention. These modules typically create `Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord` objects via `New-Object`. Specter's compatibility layer provides these types, so legacy modules work without modification when the `Specter.PssaCompatibility` module is loaded.

Modules that use `Import-LocalizedData` for string resources are also supported.

## Security Considerations

Loading a custom rule module means Specter will **execute code you provide**. The security model is designed to prevent accidental or malicious code execution, but you should understand the boundaries.

### The external rule policy

The `ExternalRules` configuration key is the central policy gate. It determines whether external code loading is permitted at all:

| Value | Behaviour |
|-------|-----------|
| `explicit` (default) | Rules load only when you pass `-CustomRulePath` or include `RulePaths` in settings. Ownership checks are enforced. |
| `disabled` | All external rule loading is blocked, even if `-CustomRulePath` is passed. Use this in locked-down environments or CI pipelines that should only run built-in rules. |
| `unrestricted` | Rules load with relaxed validation (ownership checks skipped). Only use this in development or when you fully trust the rule source. |

Set it in your JSON settings:

```json
{
    "ExternalRules": "disabled"
}
```

### File ownership checks

Before loading any external module or assembly, Specter verifies that the file and every parent directory are owned by the current user (or root/SYSTEM) and are not writable by other users. This prevents a lower-privileged attacker from substituting a malicious file at a path you've configured.

If you see ownership check failures:

```bash
# Fix permissions (Unix/macOS)
chmod 644 ./MyRules/MyRules.psm1
chmod 755 ./MyRules/
chown $USER ./MyRules/MyRules.psm1
```

### Manifest auditing

Specter audits `.psd1` manifests before importing a module. Manifests that contain any of the following fields are rejected:

- **`ScriptsToProcess`** -- executes arbitrary scripts at import time
- **`RequiredAssemblies`** -- loads arbitrary .NET assemblies
- **`TypesToProcess`** -- loads type extension files
- **`FormatsToProcess`** -- loads format definition files
- **`NestedModules`** referencing paths outside the module directory

Keep your manifest minimal. Export only your rule functions and avoid these fields entirely.

### Best practices for rule authors

1. **Ship a `.psd1` manifest** -- bare `.psm1` files work, but a manifest lets you declare exactly which functions to export and gives Specter a surface to audit.
2. **Export only `Measure-*` or `Test-*` functions** -- don't export helper functions. Use `FunctionsToExport` in the manifest to control visibility.
3. **Avoid side effects** -- rules should not write files, make network calls, or modify the environment. The restricted runspace blocks most of these, but design your rules as pure functions regardless.
4. **Don't depend on external modules** -- the rule runspace clears `$env:PSModulePath` and does not support `RequiredModules`. Everything your rule needs should be self-contained or use .NET APIs directly.
5. **Keep rules fast** -- rules that take longer than 30 seconds are killed, and three consecutive timeouts disable the rule for the session. Avoid unbounded AST traversals on large scripts.
6. **Use version control for rule modules** -- treat them as code. Review changes before deploying to shared environments.

### Best practices for consumers

1. **Prefer `explicit` mode** (the default) -- this ensures rules only load when you deliberately ask for them.
2. **Pin rule paths to version-controlled locations** -- don't point `RulePaths` at shared network drives or directories writable by other users.
3. **Use `disabled` in CI/CD** -- if your pipeline only needs built-in rules, set `ExternalRules` to `disabled` to eliminate the external code attack surface entirely.
4. **Audit rule modules before use** -- inspect the `.psm1` and `.psd1` of any third-party rule module before adding it to your configuration. Look for `Invoke-Expression`, `Add-Type`, `Start-Process`, and other side-effect-heavy patterns.
5. **Don't suppress ownership warnings** -- if Specter warns about file permissions, fix the permissions rather than switching to `unrestricted`.

## Available Commands in the Rule Runspace

Rule modules run in a restricted runspace. You have access to the full PowerShell language (object construction, .NET method calls, hashtables, etc.) but only a subset of commands is visible:

**Available**: `Get-Command`, `Get-Module`, `Get-Member`, `Get-Help`, `Where-Object`, `ForEach-Object`, `Select-Object`, `Sort-Object`, `Group-Object`, `Write-Output`, `Write-Warning`, `Write-Verbose`, `Write-Debug`, `Write-Error`, `Measure-Object`, `Compare-Object`, `Test-Path`, `Split-Path`, `Join-Path`, `Resolve-Path`, `New-Object`, `Import-LocalizedData`, `ConvertFrom-StringData`, `Out-Null`, `Out-String`.

**Not available**: File-write cmdlets, `Invoke-Expression`, `Add-Type`, `Start-Process`, network cmdlets, and others. This prevents rules from causing side effects outside the analysis.

## Tips

- Use `$Ast.FindAll({ ... }, $true)` to search the full AST tree recursively.
- Check the type of AST nodes with `-is` to find specific constructs (commands, variables, string literals, etc.).
- The `$Tokens` array gives you access to comments, whitespace, and other tokens that are not part of the AST.
- `$ScriptPath` may be `$null` when analysing a string rather than a file.
- Return diagnostics by calling `Write-Diagnostic` -- Specter collects all output objects from the rule function.
- Rules that time out (default 30 seconds) three consecutive times are disabled for the session.
- For legacy PSSA-style rules, you can return `DiagnosticRecord` objects or hashtables with `Message`, `Extent`, `RuleName`, and `Severity` keys.
