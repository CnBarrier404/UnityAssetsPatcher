# AGENTS.md

This file applies to the entire repository.

## Project Overview

UnityAssetsPatcher is a .NET 10 interactive command-line tool for inspecting, finding, and installing Unity assets file mods. It is not a Unity project; do not use Unity Editor workflows for normal development.

The solution contains:

- `src/UnityAssetsPatcher`: executable composition root and bundled resource setup.
- `src/UnityAssetsPatcher.Tui`: interactive terminal menu, prompts, pages, workflow sessions, and terminal output formatting.
- `src/UnityAssetsPatcher.Core`: shared asset contracts, asset field models, field-path matching, and general utilities.
- `src/UnityAssetsPatcher.Application`: manifest loading, query/patch/install planning, and workflow orchestration.
- `src/UnityAssetsPatcher.AssetsTools`: AssetsTools.NET integration and real Unity assets file read/write behavior.
- `tests/UnityAssetsPatcher.Tests`: xUnit v3 test project covering terminal behavior, workflows, manifest parsing, field matching, and AssetsTools integration.
- `src/UnityAssetsPatcher/Assets/AssetsRipper.tpk`: type package copied beside the executable as `resources.tpk`.

## Common Commands

Use these from the repository root:

```powershell
dotnet test UnityAssetsPatcher.sln
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

Release builds are configured in `.github/workflows/release.yml`. The release workflow publishes `win-x64` as self-contained, single-file NativeAOT by passing `-p:PublishAot=true` to `dotnet publish`; do not assume AOT settings must live in the project files.

The app is currently interactive. The main menu exposes:

- Install a mod.
- Inspect assets.
- Find assets.
- Settings.

Install always performs a preview first and only writes after confirmation.

## Architecture Notes

- `Program.cs` is the composition root. It resolves `resources.tpk` and the default `backup` directory from `AppContext.BaseDirectory`; avoid relying on the process working directory for bundled resources.
- `TerminalApp` owns the interactive menu flow, prompt handling, top-level terminal exception handling, and creation/disposal of per-run workflow sessions.
- `TerminalRenderer` owns user-facing terminal text. Keep output stable unless the requested change is explicitly about output.
- `TerminalPrompts` and `TerminalSelectionPrompt` own interactive input behavior.
- `TerminalWorkflowSessionFactory` creates per-run workflow sessions and disposes disposable assets readers after each workflow interaction.
- Domain workflow orchestration lives in `InspectAssetsWorkflow`, `FindAssetsWorkflow`, `PatchAssetsWorkflow`, and `InstallModWorkflow`, composed through `WorkflowFactory`.
- Inspect/find/patch/install behavior belongs under `src/UnityAssetsPatcher.Application`.
- Reusable install steps such as package loading, target resolution, patch planning, payload planning, and copy/apply execution belong under `Application/Modules`.
- Patch query, field patch planning, replacement planning, and output write coordination belong under `Application/Patching`.
- Shared domain models and contracts belong under `src/UnityAssetsPatcher.Core`.
- Keep AssetsTools.NET-specific behavior inside `src/UnityAssetsPatcher.AssetsTools`.
- Keep external assets file access behind `IAssetsFileReader` and `IAssetsFileWriter` so workflow and terminal code remain testable with stubs.
- Manifest loading belongs in `ModManifestLoader` and related readers under `Application/Manifests`.
- Field path/value matching belongs in `AssetFieldMatcher`.

## Manifest And Mod Package Notes

- The app accepts manifest JSON files and mod zip packages where workflows call `ModManifestLoader`.
- Patch targeting is selected by assets file name through `ManifestTargetSelector`.
- Install workflows may patch assets files and copy payload files from a mod package.
- Preserve preview behavior: preview commands should analyze and print intended changes without writing assets files or copying payloads.

## Coding Guidelines

- Keep the SDK-style project format and `net10.0` target unless the user explicitly asks to change them.
- Preserve nullable correctness, implicit usings, and file-scoped namespaces.
- Prefer `System.Text.Json` for JSON work.
- Prefer clear guard clauses and explicit error messages over broad exception handling.
- Do not introduce new NuGet packages when the BCL or existing dependencies are sufficient.
- Keep terminal I/O injectable through Spectre.Console `IAnsiConsole` where practical so tests can exercise interactive flows.
- Avoid leaking AssetsTools.NET types into `Core` or `Application`.

## Patch And Install Safety

Patch and install operations can modify real Unity game assets, so keep these safeguards intact:

- Do not allow an explicit output path to point at the input file.
- Do not overwrite an existing explicit output file.
- When overwriting the input assets file, create a backup first.
- Write through a temporary file and clean it up on failure.
- Release assets read resources before replacing files.
- Validate current field values against manifest `from` values before writing `to` values.
- Keep install previews non-mutating.
- For installs, require patch operations before applying and require payload copy destinations to be available before copying.

## Testing Expectations

- Add or update focused tests in `tests/UnityAssetsPatcher.Tests` for behavior changes.
- Prefer stubbed `IAssetsFileReader` or `IAssetsFileWriter` tests for workflow and terminal behavior.
- Use real `AssetsFileReader` / `AssetsFileWriter` tests only when the behavior depends on AssetsTools.NET integration.
- Before reporting code changes as complete, run `dotnet test UnityAssetsPatcher.sln` unless the change is documentation-only or the user asks not to run tests.

## Repository Hygiene

- Do not revert user changes or unrelated work.
- Build outputs under `bin/` and `obj/` are ignored and should not be committed.
- The current branch may be ahead of `origin/main`; do not rewrite history unless the user explicitly requests it.
