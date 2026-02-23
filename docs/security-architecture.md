# Security Architecture

This document is for contributors and reviewers working on the external rule loading pipeline. For user-facing security documentation, see [SECURITY.md](../SECURITY.md).

## Threat Model

### Attack Vectors

| Vector | Severity | Mitigation | Residual Risk |
|--------|----------|------------|---------------|
| **Path traversal** (`../../../tmp/evil.dll`) | Critical | `RulePathValidator` canonicalizes and rejects paths outside allowed roots | None if validator is always in the code path |
| **DLL planting** (attacker places `.dll` in probed directory) | Critical | No probing; absolute paths only after canonicalization | None |
| **PSModulePath poisoning** | Critical | `$env:PSModulePath` cleared in rule runspace; never consulted for rule loading | None |
| **Settings file injection** (attacker controls CWD) | High | No auto-discovery of settings files; explicit `-Settings` required | None |
| **TOCTOU race** (file swapped between validation and load) | High | Ownership check reduces window; atomicity not guaranteed | Accepted: requires attacker to win race on a file they already cannot write |
| **Module manifest abuse** (`ScriptsToProcess` etc.) | High | `ModuleManifestAuditor` rejects dangerous fields before `Import-Module` | None if auditor is always in the code path |
| **Writable-by-others file substitution** | High | `FileOwnershipValidator` checks ownership and permissions (StrictModes) | Accepted on NFS/SMB where ownership semantics differ |
| **Assembly dependency hijacking** | Medium | `RuleAssemblyLoadContext` resolves only from assembly's own directory | None for probing; assembly can still reference GAC |
| **Denial of service** (infinite loop, OOM) | Medium | Per-invocation timeout (30s); memory pressure monitoring | OOM can still crash host process |
| **Supply chain** (malicious PSGallery/NuGet package) | Medium | Ownership check; explicit opt-in | Accepted: user chose to trust the path |
| **Configuration precedence** (lower-trust source overrides higher-trust) | Medium | CLI > settings file > builder defaults; no implicit sources | None |

### Trust Level Definitions

- **Builtin**: Compiled into the Specter assembly. Fully trusted. No isolation.
- **Assembly**: Loaded from an explicit absolute path. On .NET Core/5+, loaded into an isolated `AssemblyLoadContext`. On .NET Framework 4.6.2, loaded via `Assembly.LoadFile()` in the default AppDomain. Must pass type gate (`ScriptRule` + `[Rule]`). Runs with full .NET permissions (in-process code cannot be sandboxed in modern .NET).
- **Module**: Loaded into a PowerShell runspace with restricted command visibility. Full language mode (see rationale below). Subject to per-invocation timeout.

### Assumptions

- The user's filesystem is not already fully compromised.
- The user can trust files they own and that are not writable by others.
- The user explicitly opts in to external rule loading by providing paths.

### Non-Goals

- **Full sandboxing of .NET assemblies**: .NET does not provide an in-process security boundary post-.NET Framework.
- **Automatic rule discovery**: Specter will never auto-discover rule modules from `$env:PSModulePath`, CWD, or well-known directories.

## External Rule Policy

The `ExternalRulePolicy` enum is the central policy gate. All external rule loading checks this before proceeding:

| Value | Behaviour |
|-------|-----------|
| `Explicit` (default) | Rules loaded only on explicit request; ownership checks enforced |
| `Disabled` | All external loading blocked; builder throws `InvalidOperationException` |
| `Unrestricted` | Ownership checks skipped; logs prominent warning |

The policy is set via the `ExternalRules` configuration key (JSON/PSD1) or `WithExternalRulePolicy()` on the builder.

## Validation Pipeline -- Invariants

### Stage 1: Policy Gate (`ExternalRulePolicy`)

**Invariant**: If policy is `Disabled`, no external code is loaded regardless of other inputs.

**If bypassed**: Unexpected external code execution in locked-down environments.

### Stage 2: Path Canonicalization (`RulePathValidator`)

**Invariant**: After this stage, the path is an absolute, fully-resolved path with no `..` components and no unresolved symlinks.

**If bypassed**: Attacker can use path traversal to load files outside the intended directory.

**Tests**: `RulePathValidatorTests` -- traversal patterns, symlinks, relative paths, null bytes.

**Platform note**: On .NET Framework 4.6.2, `FileInfo.ResolveLinkTarget()` is not available; symlink resolution is skipped. Symlinks are rare on Windows where net462 runs.

### Stage 3: Ownership and Permission Check (`FileOwnershipValidator`)

**Invariant**: The file and every parent directory up to the allowed root are owned by the current user or root/SYSTEM, and are not group/world-writable. Skipped when policy is `Unrestricted`.

**If bypassed**: Lower-privileged attacker can substitute malicious content at a trusted path.

**Tests**: `FileOwnershipValidatorTests` -- ownership by different user, group-writable, world-writable parent, policy bypass.

### Stage 4: Extension Classification

**Invariant**: Only `.dll`, `.psm1`, `.psd1`, and directories are accepted. Other extensions are rejected.

### Stage 5: Module Manifest Audit (`ModuleManifestAuditor`)

**Invariant**: `.psd1` files with `ScriptsToProcess`, `RequiredAssemblies`, `TypesToProcess`, `FormatsToProcess`, or external `NestedModules` are rejected before any `Import-Module` call.

**If bypassed**: Arbitrary code execution during module import.

**Tests**: `ModuleManifestAuditorTests` -- each dangerous field triggers rejection.

### Stage 6: Assembly Load Context (`RuleAssemblyLoadContext`)

**Invariant**: On .NET Core/5+, external assemblies are loaded into an isolated, collectible ALC that resolves dependencies only from the assembly's own directory. On .NET Framework, assemblies load into the default AppDomain.

**If bypassed**: Dependency hijacking; type leakage into host process.

**Tests**: `RuleAssemblyLoadContextTests` -- conflicting dependency versions stay isolated (CoreCLR only).

### Stage 7: Restricted Runspace (`ConstrainedRuleRunspaceFactory`)

**Invariant**: Module code runs in FullLanguage mode with a restricted command allowlist, no dangerous providers (FileSystem, Registry, Environment, Certificate removed), and empty `$env:PSModulePath`.

**If bypassed**: Module code can write to filesystem, access network, or modify environment.

**Tests**: Blocked commands, provider access.

## Cross-Platform Ownership Check

### Unix/macOS

- P/Invoke `lstat()` to get `st_uid`, `st_gid`, and mode bits
- `struct stat` layout varies by platform; we define platform-specific offset constants for Linux x64/ARM64 and macOS ARM64/x64
- `getuid()` / `geteuid()` for current user comparison
- `File.GetUnixFileMode()` (.NET 7+) for permission bit checks

### Windows

- `FileSystemAclExtensions.GetAccessControl()` for `FileSecurity` (deferred; currently a passthrough)
- Owner SID via `GetOwner(typeof(SecurityIdentifier))`
- Write ACL check: reject if any `Allow` rule grants `Write`/`Modify` to a SID other than current user, SYSTEM, or Administrators
- Well-known SIDs: `S-1-5-18` (SYSTEM), `S-1-5-32-544` (Administrators)

### Edge Cases

- NFS/SMB mounts: ownership semantics may differ; document as accepted risk
- Containers: root UID inside container may differ from host; ownership check still applies to container-local UIDs
- WSL: Windows files mounted in WSL show as owned by the WSL user; Linux files are accurate

## Language Mode Decision

### Why not ConstrainedLanguage mode?

ConstrainedLanguage (CLM) was the original design but was replaced with FullLanguage mode + command restrictions. Rationale:

1. **CLM only works with system lockdown**: CLM achieves its security goals when enforced system-wide via WDAC/AppLocker. Without those, any user with access to the filesystem can create a custom runspace in FullLanguage mode. In a per-runspace configuration without system lockdown, CLM is trivially bypassable.

2. **CLM prevents effective rule authoring**: Rule authors need to call .NET methods on AST nodes (`$ast.Find(...)`, `$extent.StartLineNumber`), create hashtables and custom objects, use `[regex]::Match()`, and construct diagnostic objects. CLM restricts all of these.

3. **The real boundary is opt-in**: The security decision happens when the user configures `-CustomRulePath`. Once they've decided to trust the code, language restrictions only make the code less useful without adding meaningful security.

### What we do instead

- **Command visibility**: Only safe introspection, pipeline, output, and path commands are visible. Dangerous commands (`Invoke-Expression`, `Add-Type`, `Start-Process`, filesystem writes, network) are hidden.
- **Provider removal**: FileSystem, Registry, Environment, and Certificate providers are removed to prevent side effects.
- **PSModulePath cleared**: Module code cannot import additional modules from standard paths.
- **Import-Module removed post-import**: Prevents transitive module loading during rule execution.
- **Per-invocation timeout**: 30-second default prevents infinite loops.

This gives rule authors full language power while preventing the most common side-effect vectors.

## Restricted Runspace Command Allowlist

| Command | Rationale |
|---------|-----------|
| `Get-Command`, `Get-Module`, `Get-Member`, `Get-Help` | Introspection needed by rule logic |
| `Where-Object`, `ForEach-Object`, `Select-Object`, `Sort-Object`, `Group-Object` | Pipeline manipulation |
| `Write-Output`, `Write-Warning`, `Write-Verbose`, `Write-Debug`, `Write-Error` | Output and diagnostics |
| `Measure-Object`, `Compare-Object` | Data analysis |
| `Test-Path`, `Split-Path`, `Join-Path`, `Resolve-Path` | Path inspection (read-only) |
| `New-Object` | Object construction for rule logic |
| `Import-LocalizedData`, `ConvertFrom-StringData` | String resource loading for localized rule modules |
| `Out-Null`, `Out-String` | Output formatting |

### Provider Removal

`FileSystem`, `Registry`, `Environment`, and `Certificate` providers are removed. `Function`, `Variable`, and `Alias` providers are retained for normal PowerShell operation.

Removing the FileSystem provider prevents access to the filesystem even though the language mode is Full. Without the provider, `Get-Content`, `Set-Content`, `Get-ChildItem`, etc. have no drive to operate on.

### Post-Import Lockdown

After `Import-Module`, the `Import-Module` command is removed from the session by:
1. Removing the function entry from the Function provider
2. Setting the command entry's visibility to Private in the ISS

This prevents loaded module code from importing additional modules during rule execution.

## Assembly Load Context (.NET Core/5+ only)

On .NET Core/5+, external rule assemblies are loaded into `RuleAssemblyLoadContext`:

- `isCollectible: true` enables future unloading support
- `Load()` resolves dependencies only from the assembly's own directory
- `LoadUnmanagedDll()` applies the same directory restriction
- No fallback to the default ALC probing paths

On .NET Framework 4.6.2, `AssemblyLoadContext` does not exist. Assemblies load via `Assembly.LoadFile()` in the default AppDomain. All other validation (path, ownership, type gate) still applies.

## Implementation Constraints for Contributors

1. **Every code path that accepts a user-provided path must go through `RulePathValidator`.** Direct `File.Exists()` or `Assembly.LoadFile()` calls are forbidden in the rule loading pipeline.
2. **Changes to the command allowlist require security review.** Document the rationale for any addition in this file.
3. **New policy values or relaxation flags must log a prominent warning** and be documented in SECURITY.md.
4. **Every security-relevant behaviour must have a dedicated test** that would fail if the protection were removed.
5. **The `ExternalRulePolicy` gate must be checked before any external loading.** It is the single point where all loading can be disabled.

## Security Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-02 | Use P/Invoke `lstat()` over `Mono.Posix.NETStandard` | Avoids adding a dependency; struct layouts are well-known and stable per platform |
| 2026-02 | Default ownership check to enabled | Mirrors SSH StrictModes default; security-by-default |
| 2026-02 | No auto-discovery of settings files | Prevents CWD-based injection; explicit `-Settings` required |
| 2026-02 | Remove `Import-Module` after initial load | Prevents transitive module loading during rule execution |
| 2026-02 | `AssemblyLoadContext` is collectible | Enables future unloading support; prevents permanent type leakage |
| 2026-02 | FullLanguage over ConstrainedLanguage | CLM only works with system-wide lockdown (WDAC/AppLocker); without it, CLM is bypassable and prevents useful rule authoring |
| 2026-02 | Central `ExternalRulePolicy` enum | Single policy gate for all external loading; `Disabled` value allows lockdown without removing config entries |
| 2026-02 | ALC fallback on net462 | `AssemblyLoadContext` is CoreCLR-only; net462 uses `Assembly.LoadFile()` with all other validation still active |
| 2026-02 | Add `Import-LocalizedData`, `ConvertFrom-StringData` to allowlist | Required for PSSA CommunityAnalyzerRules-style modules; read-only data loading with no side effects |
| 2026-02 | Legacy rule output duck-typing | `SanitizeOutput` accepts any object with `Message`/`Extent`/`Severity` properties, enabling `DiagnosticRecord` compat without a compile-time reference |
