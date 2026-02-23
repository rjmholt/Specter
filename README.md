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

## PowerShell Modules

Specter ships two PowerShell modules: the native **Specter** module and a **compatibility** module that provides a drop-in replacement for PSScriptAnalyzer.

### Specter Module (preferred)

The `Specter` module (`Specter.Module`) is the primary PowerShell interface. It exposes Specter-native cmdlets with an interface designed around the Specter engine rather than backward compatibility:

- **`Invoke-Specter`** -- Analyse a script file or string. Returns `ScriptDiagnostic` objects from the Specter engine directly, with full fidelity (rule metadata, corrections, extent information).
- **`Write-Diagnostic`** -- Emit a `ScriptDiagnostic` from a custom rule or pipeline. Useful for script-based rule authoring.
- **`Update-SpecterDatabase`** -- Refresh the SQLite command database from a live PowerShell session or from legacy JSON compatibility profiles.

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

Rules are configured through settings hashtables or `.psd1` files, compatible with PSScriptAnalyzer settings format:

```powershell
Invoke-ScriptAnalyzer -Path . -Settings @{
    Rules = @{
        PSAvoidUsingCmdletAliases = @{
            Allowlist = @('cd', 'ls')
        }
        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
        }
    }
}
```

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
