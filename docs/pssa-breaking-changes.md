# PSSA Breaking Changes

This document tracks intentional behavior differences between Specter compatibility rules and upstream PSScriptAnalyzer (PSSA).

## PSAvoidOverwritingBuiltInCmdlets

- **What changed:** When no explicit target profiles are configured, Specter now uses the shipped builtin cmdlet baseline (`Command.IsBuiltin`) rather than inferring behavior from the host PowerShell runtime.
- **Why:** Rule results must be stable across environments and must reflect the curated compatibility database, not whichever runtime happens to execute analysis.
- **Impact:** Scripts may report additional violations (for example commands that are builtin in the shipped baseline but not builtin on the host runtime).
- **How to control behavior:** Configure rule `PowerShellVersion` targets (or common target profiles) to scope checks to specific compatibility platforms.

## PSUseCompatibleCmdlets

- **What changed:** Specter no longer carries a hardcoded special-case for `Start-VM` and no longer forces an alternate implicit reference profile when compatibility targets explicitly match the default desktop reference.
- **Why:** Compatibility checks should be driven by imported profile data and explicit settings, not command-name exceptions or implicit reference switching.
- **Impact:** Some historical PSSA expectations (notably `Start-VM` and `Remove-Service` edge cases in legacy tests) no longer produce diagnostics under default/reference-equivalent settings.
- **How to control behavior:** Set an explicit `Reference` profile and `Compatibility` targets in rule settings when a specific cross-version comparison is required.
