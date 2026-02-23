# Specter

Core analysis engine library. This is the main project that provides:

- Script rules and configurable editors
- PowerShell AST-based analysis with no runtime dependency
- SQLite-backed command/alias/parameter database
- Formatting engine with preset styles (Default/Stroustrup, OTBS, Allman)
- Rule suppression (attribute-based and comment pragmas)
- Configuration via PSD1/JSON settings files

See the [root README](../README.md) for project overview and usage.

## Architecture

The entry point is the `ScriptAnalyzer` class, composed via `ScriptAnalyzerBuilder`:

- **`IRuleProvider`** supplies rule instances (built-in rules via `TypeRuleProvider`)
- **`IRuleExecutor`** provides execution strategy (sequential or parallel)
- **`RuleComponentProvider`** resolves dependencies injected into rule constructors (database, services)
- **`ScriptFormatter`** applies editor transformations for code formatting

Rules are C# classes annotated with `[Rule]` and `[RuleDescription]` attributes that implement `ScriptRule.AnalyzeScript()`.
