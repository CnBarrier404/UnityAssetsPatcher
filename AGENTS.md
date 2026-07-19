# AGENTS.md

## Project Overview

UnityAssetsPatcher is a .NET 10 console app for inspecting Unity assets files and installing or uninstalling mods. It provides a Terminal.Gui-based interactive UI and a non-interactive CLI.

Main projects:

- `UnityAssetsPatcher`: executable composition root and bundled resources.
- `UnityAssetsPatcher.CLI`: command-line parsing and output.
- `UnityAssetsPatcher.TUI`: interactive Terminal.Gui interface.
- `UnityAssetsPatcher.Application`: asset models and contracts, manifests, workflows, patch planning, installation, backups, and updates.
- `UnityAssetsPatcher.AssetsTools`: AssetsTools.NET integration.
- `UnityAssetsPatcher.LocalizationGenerator`: localization source generator.
- `UnityAssetsPatcher.Tests`: xUnit v3 tests.

## Common Commands

Run from the repository root:

```powershell
dotnet test UnityAssetsPatcher.slnx
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- --help
```

Use command help to discover the current CLI surface instead of relying on a duplicated command list in this file.

The executable is published as a Windows x64 NativeAOT application by `.github/workflows/release.yml`. Keep release-only version, trimming, single-file, and debug-symbol settings in the workflow unless runtime behavior requires a project-level setting.

## Architecture

- `Program.cs` is the composition root. Bundled resources and backup state are resolved from `AppContext.BaseDirectory`, not the process working directory.
- Each project registers its services through its `AddUnityAssetsPatcher*()` extension. Keep DI validation enabled.
- `IWorkflowService` is the application facade used by the CLI and TUI. Workflow implementation belongs in `Application`, not in presentation projects.
- Asset access must remain behind `IAssetsAccessScopeFactory`, `IAssetsAccessScope`, `IAssetsFileReader`, and `IAssetsFileWriter`. Keep AssetsTools.NET-specific code in `UnityAssetsPatcher.AssetsTools`.
- Manifest parsing belongs under `Application/Manifests`; patch planning under `Application/Patching`; install, uninstall, and backup transaction behavior under their corresponding `Application` directories.
- Asset-level models and contracts belong in `Application/Assets`; workflow-level contracts belong in `Application/Contracts`.

### TUI

- Interactive screens are Terminal.Gui `View` subclasses. Reusable controls belong in `TUI/Framework`; application shell and task dispatch belong in `TUI/Shell`.
- Views must not resolve dependencies through `IServiceProvider` or take `*Context` objects. Pass narrow services and callbacks explicitly.
- Do not run slow workflows on the Terminal.Gui event thread. Dispatch completion back to the UI thread and prevent overlapping mutating actions.
- All user-facing TUI text must use generated `LocalizedStrings`; do not add page-local display literals.

## Localization

- Locale JSON files live under `src/UnityAssetsPatcher.TUI/Localization/JSON/`.
- `en-US.json` defines the generated localization API; keep every locale's key set aligned with it.
- Do not edit generated localization output. The generator targets `netstandard2.0`; runtime and tests target `net10.0`.

## Manifest And Package Rules

- A package contains exactly one case-insensitive `manifest.json` and currently requires integer `schemaVersion: 1`.
- Manifest and archive reading must preserve path traversal protection, duplicate detection, extraction limits, and temporary-file cleanup.
- Optional groups are opt-in and case-insensitively unique. Reject unknown selections and conflicting effective payload destinations.
- Payloads share the resolved assets directory; reject ambiguous target directories and existing destinations.
- Keep the manifest size limit at 10 MB and total extracted content limit at 10 GB.

See `docs/mod-manifest-guide.md` for the user-facing manifest format. Update it when the contract changes.

## Mutation Safety

Install and uninstall can modify real game files. Preserve these invariants:

- Preview operations are non-mutating.
- Mutating CLI operations require explicit confirmation.
- Validate every target and expected current value before writing.
- Never overwrite an explicit output file or allow it to alias its input.
- Back up in-place changes, write through temporary files, release read handles before replacement, and clean up on failure.
- Serialize mutations with `BackupOperationLock` and persist recoverable phases through `OperationJournal`.
- Roll back partial installs and uninstalls. Startup recovery must quarantine invalid or unrecoverable state rather than treating it as installed.
- Resolve uninstall paths from the confirmed game directory plus trusted relative record paths. Reject absolute paths, traversal, reparse-point escapes, invalid records, and game fingerprint mismatches.
- Enforce reverse install order for overlapping assets files.
- Verify recorded integrity before restoration or deletion. Never delete a payload that has been modified or cannot be validated.

## Coding And Testing

Additional code style rules are in `.agents/code-style.md`.

- Keep explicit `public` modifiers on interface members.
- If tests require direct access, make the member `public`; do not add assembly metadata solely to expose internals.
- Follow the style of surrounding code and avoid unrelated refactors.
- Add focused tests for behavior changes. Prefer `StubAssetsFileService` for application and presentation tests; use fixtures under `tests/RealTestAssets/` only for AssetsTools-dependent behavior.
- When CLI behavior changes, cover parsing, output routing, exit codes, and mutation confirmation.
- When backup records or transactions change, cover integrity, path trust, install layering, rollback, and recovery.
- Run `dotnet test UnityAssetsPatcher.slnx` before reporting code changes complete. Documentation-only changes do not require the test suite.
- Update relevant README, manifest guide, or changelog content when user-facing behavior changes.
