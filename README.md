# PSpecter

A modern, high-performance PowerShell static analysis engine.

PSpecter is a reimplementation of [PSScriptAnalyzer](https://github.com/PowerShell/PSScriptAnalyzer) designed to run as a standalone library, PowerShell module, or long-lived linting daemon. It analyses PowerShell scripts using the PowerShell AST without executing any PowerShell code in the analysis engine.

## Goals and Design Principles

- **No PowerShell execution in the engine** -- all analysis is performed on the AST and token stream. The engine does not require a PowerShell runspace.
- **Embeddable** -- PSpecter is a .NET library first, usable from any C# application. PowerShell module and server modes are thin hosts around the core library.
- **Drop-in PSScriptAnalyzer compatibility** -- the `PSpecter.PssaCompatibility` module provides `Invoke-ScriptAnalyzer`, `Get-ScriptAnalyzerRule`, and `Invoke-Formatter` with the same parameter sets and output types.
- **Performance** -- rules execute in parallel, the SQLite command database provides fast lookups with an LRU cache, and the formatting engine operates on token streams.
- **Extensibility** -- rules are registered through providers, configuration is layered, and the command database is pluggable (SQLite, live PowerShell session, or hardcoded builtins).

## Architecture

```
PSpecter/                  Core analysis library (rules, formatting, configuration, command database)
PSpecter.Module/           PowerShell module exposing PSpecter cmdlets (Update-PSpecterDatabase, etc.)
PSpecter.PssaCompatibility/ Drop-in PSScriptAnalyzer-compatible PowerShell module
PSpecter.Server/           Linting daemon with gRPC and LSP endpoints
PSpecter.Test/             xUnit test suite
PSpecter.Benchmarks/       BenchmarkDotNet performance suite
```

### Core Components

- **`ScriptAnalyzer`** -- entry point for analysis. Accepts AST + tokens + configuration, returns diagnostics.
- **`ScriptFormatter`** -- entry point for formatting. Applies formatting editors to token streams.
- **`IRuleProvider`** -- supplies rules to the analyzer. `BuiltinRuleProvider` registers all built-in rules.
- **`IPowerShellCommandDatabase`** -- abstraction over command metadata lookup. Implementations: `SqliteCommandDatabase` (shipped DB), `SessionStateCommandDatabase` (live PS session), `BuiltinCommandDatabase` (fallback with hardcoded aliases).
- **`RuleComponentProviderBuilder`** -- fluent builder for wiring rules, configuration, and dependencies.

## Building

```bash
dotnet build PSpecter.sln
```

## Running Tests

```bash
# xUnit tests
dotnet test PSpecter.Test/PSpecter.Test.csproj

# Pester compatibility tests (requires pwsh and Pester 5+)
pwsh -NoProfile -Command "./Tests/RunCompatibilityTests.ps1"
```

## Using the Compatibility Module

```powershell
Import-Module ./PSpecter/out/PSpecter.PssaCompatibility/PSpecter.PssaCompatibility.psd1

# Analyze a script
Invoke-ScriptAnalyzer -Path ./MyScript.ps1

# Analyze with specific rules
Invoke-ScriptAnalyzer -Path ./MyScript.ps1 -IncludeRule PSAvoidUsingAlias

# Format a script
Invoke-Formatter -ScriptDefinition 'function foo{}'

# List available rules
Get-ScriptAnalyzerRule
```

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

PSpecter ships with a SQLite database (`pspecter.db`) containing command metadata for PowerShell 3.0 through 7.x across Windows, macOS, and Linux. This database powers rules like `AvoidPositionalParameters`, `UseCmdletCorrectly`, `AvoidUsingCmdletAliases`, and `UseCompatibleCmdlets`.

The database can be updated from a live PowerShell session:

```powershell
Import-Module PSpecter.Module
Update-PSpecterDatabase -DatabasePath ./pspecter.db -FromSession
```

Or from legacy JSON compatibility profiles:

```powershell
Update-PSpecterDatabase -DatabasePath ./pspecter.db -JsonPath ./profiles/
```

## Server Mode

PSpecter.Server provides a long-lived linting daemon with gRPC and LSP endpoints:

```bash
dotnet run --project PSpecter.Server
```

The server can be embedded in a larger C# application by referencing the `PSpecter` and `PSpecter.Server` packages and using the `AnalysisService` class directly.

## Future Goals

PSpecter aims to go beyond PSScriptAnalyzer compatibility. These are longer-term goals under research and design:

- **Variable AST metadata** -- Decorate AST variable nodes with statically determined information (evolving types, possible value sets, propagated attributes and constraints). This would enable smarter rules that understand what a variable holds at a given point in a script, using flow-sensitive or scope-based analysis.
- **DSC analysis without PowerShell invocation** -- Evaluate whether DSC configurations can be statically analysed from the AST alone using schema-driven validation, with an interface boundary that allows plugging in a PowerShell runspace when deeper evaluation is needed.

## License

MIT. See [LICENSE](PSpecter/LICENSE).
