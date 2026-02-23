# Security

## Security Model Overview

Specter is a static analysis tool for PowerShell scripts. By default, it analyses scripts without executing them, using only the PowerShell AST (Abstract Syntax Tree) and token stream.

Specter can optionally load and execute **external rule code** -- both .NET assemblies and PowerShell modules -- via the `-CustomRulePath` parameter or `RulePaths` configuration. This is the single most security-sensitive operation Specter performs. External rule loading is always an **explicit opt-in**; Specter never executes external code unless the user requests it.

### Trust hierarchy

| Level | Source | Isolation | Permissions |
|-------|--------|-----------|-------------|
| **Builtin** | Compiled into Specter | None (same process) | Full trust |
| **Assembly** | `.dll` via `-CustomRulePath` | Isolated `AssemblyLoadContext` (.NET Core only) | Full .NET (unavoidable in-process) |
| **Module** | `.psm1`/`.psd1` via `-CustomRulePath` | Restricted command visibility | Full language, limited commands |

## External Rule Policy

The `ExternalRules` configuration key controls whether Specter will load external rule code at all. This is the central policy gate.

| Value | Behaviour |
|-------|-----------|
| `explicit` (default) | External rules are loaded only when explicitly requested via `-CustomRulePath`, `RulePaths`, or the builder API. Ownership checks are enforced. |
| `disabled` | All external rule loading is blocked. `-CustomRulePath` and `RulePaths` are silently ignored. Use this in locked-down environments. |
| `unrestricted` | External rules are loaded with relaxed validation (ownership checks skipped). **Not recommended for production.** |

## External Rule Loading

When you pass a path to `-CustomRulePath` or include `RulePaths` in a settings file, Specter validates and loads the external code through a multi-stage pipeline:

1. **Policy check** -- If `ExternalRules` is `disabled`, loading is rejected immediately.
2. **Path canonicalization** -- Resolves relative paths, `..` sequences, and symlinks to a canonical absolute path. Rejects paths that escape the expected root.
3. **Ownership and permission check** -- Verifies the file and every parent directory are owned by the current user or root/SYSTEM and are not writable by other users (see StrictModes below). Skipped if `ExternalRules` is `unrestricted`.
4. **Extension classification** -- `.dll` files are loaded as .NET assemblies; `.psm1`/`.psd1` files are loaded as PowerShell modules.
5. **Type gate (assemblies)** -- Only types that extend `ScriptRule` and have a `[Rule]` attribute are accepted.
6. **Manifest audit (modules)** -- `.psd1` manifests are rejected if they contain `ScriptsToProcess`, `RequiredAssemblies`, `TypesToProcess`, or `FormatsToProcess`.
7. **Restricted execution (modules)** -- PowerShell module rules run in a runspace with restricted command visibility and per-invocation timeouts.

## Ownership and Permission Checks (StrictModes)

Modelled after SSH's `StrictModes`, Specter checks file ownership and permissions before loading external code. This prevents a lower-privileged attacker from substituting malicious content at a path the user has configured.

**What is checked** (for the rule file AND every parent directory):

- The file/directory must be **owned by the current user or root/SYSTEM**.
- The file/directory must **not be group-writable**.
- The file/directory must **not be world/other-writable**.

**On Unix/macOS**: Specter checks `st_uid` via `lstat()` and file mode bits via `File.GetUnixFileMode()`.

**On Windows**: Specter checks the file owner SID and write ACL entries via `FileSystemAclExtensions`.

**How to fix common errors**:

```bash
# Unix: fix permissions on a rule file
chmod 644 /path/to/rule.psm1
chmod 755 /path/to/rules/

# Unix: fix ownership
chown $USER /path/to/rule.psm1
```

**How to disable**: Set `ExternalRules` to `unrestricted` in your settings file. This logs a prominent warning. Disabling ownership checks means any user who can write to the rule path can execute arbitrary code through Specter.

## PowerShell Module Constraints

PowerShell module rules run in a runspace with restricted command visibility. The runspace uses **FullLanguage mode** so rule authors have access to the complete PowerShell language surface needed for effective analysis (object construction, .NET method calls, hashtable literals, etc.).

ConstrainedLanguage mode was deliberately not used because it only provides a meaningful security boundary when enforced system-wide via WDAC/AppLocker, and it prevents rule authors from performing essential operations. The real security boundary is the opt-in gate.

**Allowed commands**: `Get-Command`, `Get-Module`, `Get-Member`, `Get-Help`, `Where-Object`, `ForEach-Object`, `Select-Object`, `Sort-Object`, `Group-Object`, `Write-Output`, `Write-Warning`, `Write-Verbose`, `Write-Debug`, `Write-Error`, `Measure-Object`, `Compare-Object`, `Test-Path`, `Split-Path`, `Join-Path`, `Resolve-Path`, `New-Object`, `Import-LocalizedData`, `ConvertFrom-StringData`, `Out-Null`, `Out-String`.

**Blocked**: `Invoke-Expression`, `Add-Type`, `Start-Process`, `Start-Job`, `Invoke-Command`, `Invoke-WebRequest`, `Invoke-RestMethod`, all file-write cmdlets, environment variable access, registry access.

**Removed providers**: FileSystem, Registry, Environment, Certificate.

**Timeout**: Each rule invocation has a default 30-second timeout. Rules that time out three consecutive times are disabled for the session.

## Assembly Rule Constraints

.NET assembly rules run with full .NET permissions -- this is an inherent property of in-process .NET code and cannot be sandboxed.

On .NET Core / .NET 5+, assembly rules are loaded into an isolated, collectible `AssemblyLoadContext` to prevent dependency conflicts between rule assemblies and the host. This is not a security boundary -- it prevents version conflicts, not code execution.

On .NET Framework 4.6.2 (Windows PowerShell), `AssemblyLoadContext` is not available. Assemblies load into the default AppDomain via `Assembly.LoadFile()`. All other validation (path checks, ownership checks, type gating) still applies.

**Loading an assembly is an act of trust.** The path validation, ownership checks, and explicit opt-in requirement are the primary mitigations.

## Configuration Security

- **No auto-discovery**: Settings files are never loaded from the current working directory or well-known paths unless explicitly passed via `-Settings`.
- **Central policy gate**: The `ExternalRules` setting controls all external loading. Set to `disabled` to block external rules entirely.
- **Settings-relative resolution**: `CustomRulePath` entries in settings files are resolved relative to the settings file's directory, not the current working directory.
- **Settings file ownership**: Settings files containing `CustomRulePath` entries are subject to the same ownership/permission checks as rule files.
- **PSModulePath**: Specter never consults `$env:PSModulePath` for rule loading.

## Rule Authoring (Specter.RulePrimitives)

The `Specter.RulePrimitives` module provides cmdlets for rule authors:

- `Write-Diagnostic` -- Emits a `ScriptDiagnostic`, auto-detecting the calling rule from the call stack. Use `-CorrectionText` and `-CorrectionDescription` for simple inline fixes, or `-Correction` with pre-built `Correction` objects for advanced scenarios.
- `New-ScriptCorrection` -- Creates a `Correction` object for cases where the fix targets a different extent than the diagnostic or where multiple corrections are needed. Pass the result to `Write-Diagnostic -Correction`.

## Reporting Vulnerabilities

If you discover a security vulnerability in Specter, please report it responsibly by opening a GitHub Security Advisory at <https://github.com/rjmholt/Specter/security/advisories/new>. Do not open a public issue for security vulnerabilities.
