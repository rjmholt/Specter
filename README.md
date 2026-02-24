# Specter

A modern, high-performance PowerShell static analysis engine.

Specter is a reimplementation of [PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer) designed to run as a standalone library, PowerShell module, or long-lived linting daemon. It analyses PowerShell scripts using the PowerShell AST without executing any PowerShell code in the analysis engine.

## Goals and Design Principles

- **No PowerShell execution in the engine** -- all analysis is performed on the AST and token stream. The engine does not require a PowerShell runspace.
- **Embeddable** -- Specter is a .NET library first, usable from any C# application. PowerShell module and server modes are thin hosts around the core library.
- **Drop-in PSScriptAnalyzer compatibility** -- the `Specter.PssaCompatibility` module provides `Invoke-ScriptAnalyzer`, `Get-ScriptAnalyzerRule`, and `Invoke-Formatter` with the same parameter sets and output types.
- **Performance** -- rules execute in parallel, the SQLite command database provides fast lookups with an LRU cache, and the formatting engine operates on token streams.
- **Extensibility** -- rules are registered through providers, configuration is layered, and the command database is pluggable (SQLite, live PowerShell session, or hardcoded builtins).

## Architecture

```
Specter/                   Core analysis library (rules, formatting, configuration, command database)
Specter.Module/            PowerShell module exposing Specter cmdlets (Update-SpecterDatabase, etc.)
Specter.RuleCmdlets/      C# module providing cmdlets for custom rule authors
Specter.PssaCompatibility/ Drop-in PSScriptAnalyzer-compatible PowerShell module
Specter.Server/            Linting daemon with gRPC and LSP endpoints
Specter.Test/              xUnit test suite
Specter.Benchmarks/        BenchmarkDotNet performance suite
```

### Core Components

- **`ScriptAnalyzer`** -- entry point for analysis. Accepts AST + tokens + configuration, returns diagnostics.
- **`ScriptFormatter`** -- entry point for formatting. Applies formatting editors to token streams.
- **`IRuleProvider`** -- supplies rules to the analyzer. `BuiltinRuleProvider` registers all built-in rules.
- **`IPowerShellCommandDatabase`** -- abstraction over command metadata lookup. Implementations: `SqliteCommandDatabase` (shipped DB), `SessionStateCommandDatabase` (live PS session), `BuiltinCommandDatabase` (fallback with hardcoded aliases).
- **`RuleComponentProviderBuilder`** -- fluent builder for wiring rules, configuration, and dependencies.

## Building

The unified build script compiles all assemblies and stages the three PowerShell modules under `out/`:

```powershell
# Debug build (default)
./build.ps1

# Release build with NuGet package
./build.ps1 -Configuration Release -Pack -Clean

# Import the built module
Import-Module ./out/Specter/Specter.psd1
```

Or build individual projects with `dotnet` directly:

```bash
dotnet build Specter.sln
```

## Running Tests

```bash
# xUnit tests
dotnet test Specter.Test/Specter.Test.csproj

# Pester compatibility tests (requires pwsh and Pester 5+)
pwsh -NoProfile -Command "./Tests/RunCompatibilityTests.ps1"
```

## C# Rule Authoring Package

C# rule authors should reference the thin `Specter.Api` package rather than `Specter`:

```bash
dotnet add package Specter.Api
dotnet add package System.Management.Automation
```

`Specter.Api` contains rule authoring contracts and diagnostics types without pulling in engine/runtime dependencies like SQLite or JSON configuration components.

## Publishing

The publish script builds in Release mode, runs tests, and pushes to the PowerShell Gallery and nuget.org:

```powershell
# Publish everything (requires API keys)
./publish.ps1 -PSGalleryApiKey $galleryKey -NuGetApiKey $nugetKey

# Or use environment variables
$env:PSGALLERY_API_KEY = '...'
$env:NUGET_API_KEY = '...'
./publish.ps1

# Dry run
./publish.ps1 -WhatIf

# PSGallery only (skip NuGet)
./publish.ps1 -PSGalleryApiKey $key -SkipNuGet
```

The current version is **0.1.0-preview.1**, signalling that the API is not yet stable.

## PowerShell Modules

Specter ships two PowerShell modules: the native **Specter** module and a **compatibility** module that provides a drop-in replacement for PSScriptAnalyzer.

### Specter Module (preferred)

The `Specter` module (`Specter.Module`) is the primary PowerShell interface. It exposes Specter-native cmdlets with an interface designed around the Specter engine rather than backward compatibility:

- **`Invoke-Specter`** -- Analyse a script file or string. Returns `ScriptDiagnostic` objects from the Specter engine directly, with full fidelity (rule metadata, corrections, extent information).
- **`Update-SpecterDatabase`** -- Refresh the SQLite command database from a live PowerShell session or from legacy JSON compatibility profiles.

The **Specter.RuleCmdlets** module provides cmdlets for custom rule authors:

- **`Write-Diagnostic`** -- Emit a `ScriptDiagnostic` from a rule function, auto-detecting the calling rule from the call stack. Use `-CorrectionText` for simple inline fixes.
- **`New-ScriptCorrection`** -- Create a `Correction` object for advanced scenarios (different extent or multiple corrections). Pass to `Write-Diagnostic -Correction`.

See [Writing Custom Rules](docs/writing-rules.md) for a full guide with examples.

```powershell
Import-Module ./Specter/out/Specter/Specter.psd1

Invoke-Specter -Path ./MyScript.ps1
Update-SpecterDatabase -DatabasePath ./specter.db -FromSession
```

### PSScriptAnalyzer Compatibility Module

The `Specter.PssaCompatibility` module is a drop-in replacement for [PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer). It wraps the Specter engine behind the same cmdlet names, parameter sets, and output types that PSScriptAnalyzer users expect, so existing scripts, editor integrations, and CI pipelines continue to work without changes.

The module exports:

- **`Invoke-ScriptAnalyzer`** -- same parameters and output shape as the original.
- **`Get-ScriptAnalyzerRule`** -- lists available rules.
- **`Invoke-Formatter`** -- formats PowerShell scripts.

```powershell
Import-Module ./Specter/out/Specter.PssaCompatibility/Specter.PssaCompatibility.psd1

# Analyze a script
Invoke-ScriptAnalyzer -Path ./MyScript.ps1

# Analyze with specific rules
Invoke-ScriptAnalyzer -Path ./MyScript.ps1 -IncludeRule PSAvoidUsingAlias

# Format a script
Invoke-Formatter -ScriptDefinition 'function foo{}'

# List available rules
Get-ScriptAnalyzerRule
```

If you are starting fresh, prefer the Specter module. Use the compatibility module when you need to swap in Specter behind existing PSScriptAnalyzer-based tooling.

## Configuration

Specter is configured through a JSON file (preferred) or through PowerShell `.psd1` files for PSScriptAnalyzer compatibility.

### JSON Configuration (preferred)

Create a `specter.json` file in your project root:

```json
{
    "BuiltinRulePreference": "default",
    "RuleExecutionMode": "parallel",
    "Rules": {
        "PSAvoidUsingCmdletAliases": {
            "AllowList": ["cd", "ls"]
        },
        "PSUseConsistentIndentation": {
            "IndentationSize": 4,
            "PipelineIndentation": "IncreaseIndentationForFirstPipeline"
        },
        "PSAvoidUsingWriteHost": {
            "Enable": false
        }
    }
}
```

Rules are enabled by default -- including a rule in the `Rules` section with configuration is enough to activate it. Set `"Enable": false` to explicitly disable a rule.

Top-level settings:

| Key | Values | Description |
|-----|--------|-------------|
| `BuiltinRulePreference` | `"none"`, `"default"`, `"comprehensive"` | Which built-in rules to load |
| `RuleExecutionMode` | `"default"`, `"parallel"`, `"sequential"` | How rules are executed |
| `RulePaths` | `["/path/to/rules", ...]` | Additional rule module paths (see security note below) |
| `ExternalRules` | `"explicit"`, `"disabled"`, `"unrestricted"` | External rule loading policy (default: `explicit`) |
| `Rules` | `{ "RuleName": { ... }, ... }` | Per-rule configuration |

Each rule entry under `Rules` supports these shared keys alongside any rule-specific settings:

| Key | Type | Description |
|-----|------|-------------|
| `Enable` | `bool` | Disable the rule (`false`); defaults to `true` |
| `ExcludePaths` | `string[]` | Glob patterns for paths to skip |

Pass the configuration file to `Invoke-Specter` or the server:

```powershell
Invoke-Specter -Path ./MyScript.ps1 -Settings ./specter.json
```

```bash
specter-server lsp --config ./specter.json
```

### PSD1 Configuration (PSScriptAnalyzer compatibility)

The compatibility module also accepts `.psd1` settings files and hashtables, matching the PSScriptAnalyzer settings format:

```powershell
Invoke-ScriptAnalyzer -Path . -Settings @{
    Rules = @{
        PSAvoidUsingCmdletAliases = @{
            AllowList = @('cd', 'ls')
        }
        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
        }
    }
}
```

### Custom Rule Security

Loading external rules means executing third-party code in your process. Specter applies several safeguards automatically:

- **Explicit opt-in** -- external rules are never loaded unless you pass `-CustomRulePath` or include `RulePaths` in your settings. Set `"ExternalRules": "disabled"` to block all external loading (recommended for CI/CD when only built-in rules are needed).
- **File ownership checks** -- rule files and their parent directories must be owned by the current user or root/SYSTEM and must not be writable by other users.
- **Manifest auditing** -- `.psd1` manifests are rejected if they contain `ScriptsToProcess`, `RequiredAssemblies`, or other fields that execute code at import time.
- **Restricted runspace** -- PowerShell module rules run with a limited command allowlist and no access to the filesystem, registry, or network providers.

See [SECURITY.md](SECURITY.md) for the full threat model and [Writing Custom Rules](docs/writing-rules.md) for best practices.

## Command Database

Specter ships with a SQLite database (`specter.db`) containing command metadata for PowerShell 3.0 through 7.x across Windows, macOS, and Linux. This database powers rules like `AvoidPositionalParameters`, `UseCmdletCorrectly`, `AvoidUsingCmdletAliases`, and `UseCompatibleCmdlets`.

The database can be updated from a live PowerShell session:

```powershell
Import-Module Specter.Module
Update-SpecterDatabase -DatabasePath ./specter.db -FromSession
```

Or from legacy JSON compatibility profiles:

```powershell
Update-SpecterDatabase -DatabasePath ./specter.db -JsonPath ./profiles/
```

## Server Mode

Specter.Server provides a long-lived linting daemon with gRPC and LSP endpoints:

```bash
dotnet run --project Specter.Server
```

The server can be embedded in a larger C# application by referencing the `Specter` and `Specter.Server` packages and using the `AnalysisService` class directly.

## Future Goals

Specter aims to go beyond PSScriptAnalyzer compatibility. These are longer-term goals under research and design:

- **Variable AST metadata** -- Decorate AST variable nodes with statically determined information (evolving types, possible value sets, propagated attributes and constraints). This would enable smarter rules that understand what a variable holds at a given point in a script, using flow-sensitive or scope-based analysis.
- **DSC analysis without PowerShell invocation** -- Evaluate whether DSC configurations can be statically analysed from the AST alone using schema-driven validation, with an interface boundary that allows plugging in a PowerShell runspace when deeper evaluation is needed.

## License

MIT. See [LICENSE](Specter/LICENSE).
