# AGENTS.md

This file applies to the entire repository.

## Project Overview

UnityAssetsPatcher is a .NET 10 command-line tool for installing and uninstalling Unity assets file mods. It provides an interactive terminal UI and non-interactive CLI commands. It is not a Unity project; do not use Unity Editor workflows for normal development.

The solution contains:

- `src/UnityAssetsPatcher`: executable composition root and bundled resource setup.
- `src/UnityAssetsPatcher.CLI`: non-interactive command-line parsing and commands, including manifest validation.
- `src/UnityAssetsPatcher.TUI`: interactive terminal menu, prompts, pages, terminal UI framework, and terminal output formatting.
- `src/UnityAssetsPatcher.Core`: shared asset contracts, asset field models, field-path matching, and general utilities.
- `src/UnityAssetsPatcher.Application`: manifest loading, query/patch/install/uninstall planning, and workflow orchestration.
- `src/UnityAssetsPatcher.AssetsTools`: AssetsTools.NET integration and real Unity assets file read/write behavior.
- `src/UnityAssetsPatcher.LocalizationGenerator`: Roslyn source generator that creates strongly typed localization accessors and validates locale JSON files at build time.
- `tests/UnityAssetsPatcher.Tests`: xUnit v3 test project covering terminal behavior, install/uninstall workflows, manifest parsing, field matching, and AssetsTools integration.
- `src/UnityAssetsPatcher/Assets/AssetsRipper.tpk`: type package copied beside the executable as `resources.tpk`.

## Common Commands

Use these from the repository root:

```powershell
dotnet test UnityAssetsPatcher.sln
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- check
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- check --config <manifest-or-mod-zip-path>
```

`check` validates `./manifest.json` by default. Use `--config` or `-c` to validate another manifest JSON file or mod zip package. It returns exit code `0` on success, `1` when validation fails, and `2` for command-line parse errors.

`src/UnityAssetsPatcher/UnityAssetsPatcher.csproj` enables NativeAOT with `PublishAot`. Release builds are configured in `.github/workflows/release.yml`, which restores and tests the solution, publishes `win-x64` as a self-contained single-file NativeAOT executable, packages it with `resources.tpk`, generates a SHA-256 checksum, and creates a GitHub Release from a `v*.*.*` tag. Keep release-only version, trimming-warning, single-file, and debug-symbol settings in the workflow unless the runtime behavior itself requires a project-level setting.

When invoked without arguments, the app starts the interactive TUI. Its main menu is page-based and currently exposes:

- Install a mod.
- Uninstall a mod.
- Settings.

Install and uninstall both perform a preview first and only write after confirmation.

Before showing the TUI, the app recovers or quarantines incomplete install/uninstall transactions. When arguments are present, it dispatches directly to the CLI instead. The main menu performs a best-effort check of the latest GitHub release; network, timeout, I/O, and malformed-response failures must remain non-fatal.

## Architecture Notes

- `Program.cs` is the composition root. It resolves `resources.tpk` and the default `backup` directory from `AppContext.BaseDirectory`, builds the service provider, and dispatches to `CLIApplication` when arguments are present. Without arguments, it calls `ModBackupStore.RecoverPendingTransactions()` and then runs `TerminalApp`; avoid relying on the process working directory for bundled resources or backup state.
- Each project provides a `*ServiceCollectionExtensions` class with a single `AddUnityAssetsPatcher*()` method. `Program.cs` chains them in order: `AddUnityAssetsPatcherAssetsTools` → `AddUnityAssetsPatcherApplication` → `AddUnityAssetsPatcherCLI` → `AddUnityAssetsPatcherTUI`. The service provider is built with `ValidateOnBuild` and `ValidateScopes` enabled.
- `CLIApplication` owns root-command parsing, command registration through `ICLICommand`, output/error routing, and parse-error exit behavior. `CheckCLICommand` validates a manifest JSON file or mod zip through `ModManifestReader` without running install or uninstall workflows.
- `TerminalApp` owns top-level terminal exception handling and cursor visibility management, then delegates menu flow to `TerminalNavigator`.
- `TerminalNavigator` performs the startup update check, runs the main navigation loop, renders the main menu via `MainMenuTerminalInput`/`MainMenuTerminalView`, dispatches to `ITerminalPage` implementations, and catches per-page exceptions.
- `TerminalUI` is the rendering facade aggregating `TerminalLayout`, `TerminalText`, `TerminalList`, `TerminalSummary`, and `TerminalStatus` under `Framework/`.
- `TerminalPrompts` and `TerminalSelectionPrompt` own interactive input behavior.
- TUI pages follow an MVP-like pattern: each page is split into `*TerminalPage` (controller/orchestrator implementing `ITerminalPage`), `*TerminalInput` (user input handling), and `*TerminalView` (output rendering). No class under `TUI.Pages` may take `IServiceProvider` or a `*Context` type in its constructor. `ITerminalPage` implementations must also avoid `IAnsiConsole`, `TerminalUI`, and `TerminalPrompts`, and views must not take `IAnsiConsole` directly; reflection tests enforce these boundaries.
- Domain workflow orchestration lives in `InstallModWorkflow` and `UninstallModWorkflow`. Patch behavior is composed from services under `Application/Patching`; there is no standalone patch workflow exposed by the TUI.
- `WorkflowFactory` manually composes workflows and per-operation patch services from an `IAssetsAccessScope`. `IWorkflowService`, `WorkflowFactory`, and `IAssetsAccessScopeFactory` are DI-managed singletons; each install/preview-install facade operation creates and disposes an assets access scope, while uninstall operations do not require assets access.
- `IWorkflowService` / `WorkflowService` is the application facade that delegates to workflow instances via `WorkflowFactory`. Public contracts live under `Application/Contracts/`, split across `InstallWorkflowContracts`, `WorkflowRequests`, `WorkflowResults`, `UpdateContracts`, and `ModManifest`.
- Patch/install/uninstall behavior belongs under `src/UnityAssetsPatcher.Application/Installation`, `src/UnityAssetsPatcher.Application/Uninstallation`, and `src/UnityAssetsPatcher.Application/Patching`.
- Reusable install steps such as package/archive loading, target resolution, patch and payload planning, timed execution, rollback, and record creation belong under `Application/Installation`.
- Patch query, field patch planning, replacement planning, and output write coordination belong under `Application/Patching`.
- Backup records, integrity hashes, game-instance identity, install ordering, operation locking/journaling, and startup recovery belong under `Application/Backups`.
- Update contracts belong in `Application/Contracts`; the GitHub implementation belongs under `Application/Updates`. The TUI currently registers its `HttpClient` and `IUpdateChecker` because update results are displayed by the main menu.
- Shared domain models and contracts belong under `src/UnityAssetsPatcher.Core`.
- Keep AssetsTools.NET-specific behavior inside `src/UnityAssetsPatcher.AssetsTools`.
- Keep external assets file access behind `IAssetsFileReader` and `IAssetsFileWriter` so workflow and terminal code remain testable with stubs.
- Manifest reading, JSON parsing, and optional-group selection belong under `Application/Manifests`; `ModManifestReader` is the entry point used by install planning.
- Field path/value matching belongs in `AssetFieldMatcher`.
- All TUI-facing strings go through the generated `LocalizedStrings` class. Do not edit generated output or bypass it with page-local literals.

## Localization

- Locale files are embedded JSON resources under `src/UnityAssetsPatcher.TUI/Localization/JSON/`. `en-US.json` is the primary/default locale and `zh-CN.json` is the Simplified Chinese locale.
- `JsonLocalizationProvider` loads English first, overlays the current UI culture (walking parent cultures), and falls back to the localization key when a string is absent.
- `UnityAssetsPatcher.LocalizationGenerator` consumes the locale JSON files as `AdditionalFiles`, generates `LocalizedStrings.g.cs` from the `en-US` keys, and reports diagnostics for missing or extra translated keys. Keep locale key sets aligned.
- The generator targets `netstandard2.0` as a Roslyn component; the runtime and test projects target `net10.0`.

## Manifest And Mod Package Notes

- The install workflow accepts mod zip packages containing exactly one case-insensitive `manifest.json`; manifest JSON parsing is performed through `ModManifestReader` and its specialized readers.
- Every manifest requires an integer `schemaVersion`; the currently supported version is `1`, and missing or unsupported versions are rejected.
- Patch targeting is selected by assets file name through `TargetAssetResolver`.
- A patch may use `componentType` with a `GameObject` target to select a mounted component by type. The former `component` property has been renamed and is rejected; `componentType` cannot be combined with whole-asset replacement.
- Install workflows may apply field-level `set`/`add` operations, replace whole assets from source assets files in the package, and copy payload files.
- Preserve preview behavior: preview commands should analyze and print intended changes without writing assets files, copying payloads, restoring backups, deleting payloads, or updating install records.
- `ModBackupStore` persists versioned `record.json` files in timestamped backup directories. Records store a game-instance fingerprint, monotonic install sequence, relative game/backup paths, and SHA-256/length integrity data. A valid record directory represents an installed mod; successful uninstall deletes that directory instead of writing an uninstalled status.
- `GameDirectoryResolver` resolves a game's install directory by scanning Steam library roots for `appmanifest_*.acf` files matching the game name, parsing VDF key-value format.
- `ModPackage.Open` extracts only required package entries into a temporary directory, enforces safe archive paths and the total extraction limit, and cleans up on dispose.
- A manifest `optional` array declares opt-in content groups. `ModPackage.Open` merges selected groups through the pure `ModManifestOptionalSelector.SelectOptional` extension before downstream planning. Optional group names are case-insensitively unique, unknown selections fail, and the effective payload file names must remain unique because all payloads share one destination directory. Preview exposes available groups; applied names are saved in `InstallRecord.OptionalGroups` (omitted when none) and returned in `InstallModResult.OptionalGroups`.
- Payload files are copied beside the resolved target assets files. Payload-bearing manifests require all patch targets to resolve to one directory, and existing destinations are rejected.
- Keep the package limits intact: `manifest.json` is capped at 10 MB and total extracted content at 10 GB.

## Coding Guidelines

Additional repository-specific code style rules are defined in `.agents/code-style.md` and apply throughout the repository.

## Patch, Install, And Uninstall Safety

Patch, install, and uninstall operations can modify real Unity game assets, so keep these safeguards intact:

- Do not allow an explicit output path to point at the input file.
- Do not overwrite an existing explicit output file.
- When overwriting the input assets file, create a backup first.
- Write through a temporary file and clean it up on failure.
- Release assets read resources before replacing files.
- Validate current field values against manifest `from` values before writing `to` values.
- Keep install and uninstall previews non-mutating.
- Serialize mutating install/uninstall operations with `BackupOperationLock`, persist phase changes through `OperationJournal`, and preserve startup recovery for interrupted transactions.
- For installs, validate every patch and payload destination before mutation, apply patch operations before copying payloads, and roll back already-applied patches/files on failure.
- Resolve uninstall paths from the caller-confirmed game directory plus trusted relative record paths; reject absolute paths, traversal, reparse-point escapes, mismatched game fingerprints, and invalid record formats.
- Enforce install layering: when installed mods overlap an assets file, uninstall must proceed in reverse install sequence. Non-overlapping mods may be removed independently.
- Before mutation, validate that patched assets and backups exist and match recorded integrity, and that copied payloads are either missing or unchanged. A modified/unreadable payload must block cleanup rather than be deleted.
- During uninstall, stage payload copies and keep per-attempt rollback backups for assets so any later failure can restore the pre-uninstall state.
- Restore assets through temporary files and clean them up on failure.
- Delete copied payload files only after integrity validation and only when they still exist.
- Invalid records or unrecoverable pending operations discovered at startup are quarantined; do not silently treat them as valid installed mods.

## Testing Expectations

- Add or update focused tests in `tests/UnityAssetsPatcher.Tests` for behavior changes.
- Add focused CLI tests for command parsing, output/error routing, exit codes, default paths, and explicit options when changing CLI behavior.
- Prefer stubbed `IAssetsFileReader` or `IAssetsFileWriter` tests for workflow and terminal behavior.
- Use real `AssetsFileReader` / `AssetsFileWriter` tests only when the behavior depends on AssetsTools.NET integration.
- Before reporting code changes as complete, run `dotnet test UnityAssetsPatcher.sln` unless the change is documentation-only or the user asks not to run tests.
- `StubAssetsFileService` implements `IAssetsFileReader`, `IAssetsFileWriter`, and `IAssetsAccessScope` in one class; use it as the unified test double for workflow and TUI tests.
- `tests/RealTestAssets/` contains real Unity assets fixtures for integration tests that depend on actual AssetsTools.NET parsing.
- Reflection-based architectural assertions enforce constructor dependency rules for TUI page controllers, inputs, and views.
- Backup-focused tests cover install sequence/layer safety, record integrity, trusted uninstall path resolution, and interrupted-operation recovery; update these when changing record or transaction behavior.

## Repository Hygiene

- Do not revert user changes or unrelated work.
- Build outputs under `bin/` and `obj/` are ignored and should not be committed.
- The current branch may be ahead of `origin/main`; do not rewrite history unless the user explicitly requests it.
