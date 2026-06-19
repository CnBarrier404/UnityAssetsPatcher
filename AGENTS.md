# AGENTS.md

This file applies to the entire repository.

## Project Overview

UnityAssetsPatcher is a .NET 10 interactive command-line tool for inspecting, finding, installing, and uninstalling Unity assets file mods. It is not a Unity project; do not use Unity Editor workflows for normal development.

The solution contains:

- `src/UnityAssetsPatcher`: executable composition root and bundled resource setup.
- `src/UnityAssetsPatcher.TUI`: interactive terminal menu, prompts, pages, terminal UI framework, and terminal output formatting.
- `src/UnityAssetsPatcher.Core`: shared asset contracts, asset field models, field-path matching, and general utilities.
- `src/UnityAssetsPatcher.Application`: manifest loading, query/patch/install/uninstall planning, and workflow orchestration.
- `src/UnityAssetsPatcher.AssetsTools`: AssetsTools.NET integration and real Unity assets file read/write behavior.
- `tests/UnityAssetsPatcher.Tests`: xUnit v3 test project covering terminal behavior, install/uninstall workflows, manifest parsing, field matching, and AssetsTools integration.
- `src/UnityAssetsPatcher/Assets/AssetsRipper.tpk`: type package copied beside the executable as `resources.tpk`.

## Common Commands

Use these from the repository root:

```powershell
dotnet test UnityAssetsPatcher.sln
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

Release builds are configured in `.github/workflows/release.yml`. The release workflow publishes `win-x64` as self-contained, single-file NativeAOT by passing `-p:PublishAot=true` to `dotnet publish`; do not assume AOT settings must live in the project files.

The app is currently interactive. The main menu is page-based and currently exposes:

- Install a mod.
- Uninstall a mod.
- Settings.

Install and uninstall both perform a preview first and only write after confirmation.

## Architecture Notes

- `Program.cs` is the composition root. It resolves `resources.tpk` and the default `backup` directory from `AppContext.BaseDirectory`; avoid relying on the process working directory for bundled resources.
- Each project provides a `*ServiceCollectionExtensions` class with a single `AddUnityAssetsPatcher*()` method. `Program.cs` chains them in order: `AddUnityAssetsPatcherAssetsTools` → `AddUnityAssetsPatcherApplication` → `AddUnityAssetsPatcherTUI`. The service provider is built with `ValidateOnBuild` and `ValidateScopes` enabled.
- `TerminalApp` owns top-level terminal exception handling and cursor visibility management, then delegates menu flow to `TerminalNavigator`.
- `TerminalNavigator` runs the main navigation loop, renders the main menu via `MainMenuTerminalInput`/`MainMenuTerminalView`, dispatches to `ITerminalPage` implementations, and catches per-page exceptions.
- `TerminalUI` is the rendering facade aggregating `TerminalLayout`, `TerminalText`, `TerminalList`, `TerminalTable`, `TerminalSummary`, and `TerminalStatus` under `Framework/`.
- `TerminalPrompts` and `TerminalSelectionPrompt` own interactive input behavior.
- TUI pages follow an MVP-like pattern: each page is split into three classes — `*TerminalPage` (controller/orchestrator implementing `ITerminalPage`), `*TerminalInput` (user input handling), and `*TerminalView` (output rendering). Page constructors are forbidden from depending on `IAnsiConsole`, `TerminalUI`, `TerminalPrompts`, `IServiceProvider`, or any `*Context` type — enforced by reflection tests.
- Domain workflow orchestration lives in `FindAssetsWorkflow`, `PatchAssetsWorkflow`, `InstallModWorkflow`, and `UninstallModWorkflow`, composed through `WorkflowFactory`.
- `WorkflowFactory` manually composes workflow instances with their dependencies using the provided `IAssetsAccessScope`. Only the top-level `IWorkflowService` and `IAssetsAccessScopeFactory` are DI-managed singletons.
- `IWorkflowService` / `WorkflowService` is the application facade that delegates to workflow instances via `WorkflowFactory`. Request and response types live in `Application/Contracts/` as `WorkflowRequests` and `WorkflowResults`.
- Find/patch/install/uninstall behavior belongs under `src/UnityAssetsPatcher.Application`.
- Reusable install steps such as package loading, target resolution, patch planning, payload planning, and copy/apply execution belong under `Application/Modules`.
- Patch query, field patch planning, replacement planning, and output write coordination belong under `Application/Patching`.
- Shared domain models and contracts belong under `src/UnityAssetsPatcher.Core`.
- Keep AssetsTools.NET-specific behavior inside `src/UnityAssetsPatcher.AssetsTools`.
- Keep external assets file access behind `IAssetsFileReader` and `IAssetsFileWriter` so workflow and terminal code remain testable with stubs.
- Manifest loading belongs in `ModManifestLoader` and related readers under `Application/Manifests`.
- Field path/value matching belongs in `AssetFieldMatcher`.
- All user-facing strings go through `LocalizedStrings` resource files. English is the default, Simplified Chinese is the secondary locale.

## Manifest And Mod Package Notes

- The app accepts manifest JSON files and mod zip packages where workflows call `ModManifestLoader`.
- Patch targeting is selected by assets file name through `ManifestTargetSelector`.
- Install workflows may patch assets files and copy payload files from a mod package.
- Preserve preview behavior: preview commands should analyze and print intended changes without writing assets files, copying payloads, restoring backups, deleting payloads, or updating install records.
- `ModInstallationStore` persists install records as `record.json` files in timestamped backup directories, tracking `Installed`/`Uninstalled` status for uninstall support.
- `GameDirectoryResolver` resolves a game's install directory by scanning Steam library roots for `appmanifest_*.acf` files matching the game name, parsing VDF key-value format.
- `PackageWorkspace` extracts source assets files from the mod zip into a temp directory when patches use `replaceAsset`, and cleans up on dispose.

## Coding Guidelines

- Keep the SDK-style project format and `net10.0` target unless the user explicitly asks to change them.
- Preserve nullable correctness, implicit usings, and file-scoped namespaces.
- Prefer `System.Text.Json` for JSON work.
- Prefer clear guard clauses and explicit error messages over broad exception handling.
- Do not introduce new NuGet packages when the BCL or existing dependencies are sufficient.
- Keep terminal I/O injectable through Spectre.Console `IAnsiConsole` where practical so tests can exercise interactive flows.
- Avoid leaking AssetsTools.NET types into `Core` or `Application`.

## Patch, Install, And Uninstall Safety

Patch, install, and uninstall operations can modify real Unity game assets, so keep these safeguards intact:

- Do not allow an explicit output path to point at the input file.
- Do not overwrite an existing explicit output file.
- When overwriting the input assets file, create a backup first.
- Write through a temporary file and clean it up on failure.
- Release assets read resources before replacing files.
- Validate current field values against manifest `from` values before writing `to` values.
- Keep install and uninstall previews non-mutating.
- For installs, require patch operations before applying and require payload copy destinations to be available before copying.
- Before uninstalling, require the install record to be installed and validate that every patched assets file and install backup exists.
- During uninstall, copy the current assets file into an `uninstall` backup directory before restoring the install backup.
- Restore uninstall backups through a temporary file and clean it up on failure.
- Delete copied payload files only when they still exist.

## Testing Expectations

- Add or update focused tests in `tests/UnityAssetsPatcher.Tests` for behavior changes.
- Prefer stubbed `IAssetsFileReader` or `IAssetsFileWriter` tests for workflow and terminal behavior.
- Use real `AssetsFileReader` / `AssetsFileWriter` tests only when the behavior depends on AssetsTools.NET integration.
- Before reporting code changes as complete, run `dotnet test UnityAssetsPatcher.sln` unless the change is documentation-only or the user asks not to run tests.
- `StubAssetsFileService` implements `IAssetsFileReader`, `IAssetsFileWriter`, and `IAssetsAccessScope` in one class; use it as the unified test double for workflow and TUI tests.
- `tests/RealTestAssets/` contains real `.assets` and `.resource` files for integration tests that depend on actual AssetsTools.NET parsing.
- Reflection-based architectural assertions enforce constructor dependency rules for TUI page classes.

## Repository Hygiene

- Do not revert user changes or unrelated work.
- Build outputs under `bin/` and `obj/` are ignored and should not be committed.
- The current branch may be ahead of `origin/main`; do not rewrite history unless the user explicitly requests it.
