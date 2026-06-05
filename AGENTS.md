# AGENTS.md

This file applies to the entire repository.

## Project Overview

UnityAssetsPatcher is a .NET 10 interactive command-line tool for inspecting, finding, patching, and installing Unity assets file mods. It is not a Unity project; do not use Unity Editor workflows for normal development.

The solution contains:

- `src/UnityAssetsPatcher`: executable entry point, interactive terminal menu, prompts, and terminal output formatting.
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

The app is currently interactive. The main menu exposes:

- Install a mod.
- Preview a mod install.
- Inspect assets.
- Find assets.
- Patch assets.

## Architecture Notes

- `Program.cs` is the composition root. It resolves `resources.tpk` and the default `backup` directory from `AppContext.BaseDirectory`; avoid relying on the process working directory for bundled resources.
- `TerminalApp` owns the interactive menu flow, prompt handling, top-level terminal exception handling, and creation/disposal of per-run workflow service scopes.
- `TerminalOutputFormatter` owns user-facing terminal text. Keep output stable unless the requested change is explicitly about output.
- Domain workflow orchestration lives in `AssetsWorkflowService`, which composes inspect, find, patch, and install workflows.
- Inspect/find/patch/install behavior belongs under `src/UnityAssetsPatcher.Application`.
- Shared domain models and contracts belong under `src/UnityAssetsPatcher.Core`.
- Keep AssetsTools.NET-specific behavior inside `src/UnityAssetsPatcher.AssetsTools`.
- Keep external assets file access behind `IAssetsReader`, `IAssetsPatchWriter`, and `IAssetsFileService` so workflow and terminal code remain testable with stubs.
- Manifest loading belongs in `ModManifestLoader` and related readers under `Application/Manifests`.
- Field path/value matching belongs in `AssetFieldMatcher`.

## Manifest And Mod Package Notes

- The app accepts manifest JSON files and mod zip packages where workflows call `ModManifestLoader`.
- Patch targeting is selected by assets file name through `PatchTargetSelector`.
- Install workflows may patch assets files and copy payload files from a mod package.
- Preserve preview behavior: preview commands should analyze and print intended changes without writing assets files or copying payloads.

## Coding Guidelines

- Keep the SDK-style project format and `net10.0` target unless the user explicitly asks to change them.
- Preserve nullable correctness, implicit usings, and file-scoped namespaces.
- Prefer `System.Text.Json` for JSON work.
- Prefer clear guard clauses and explicit error messages over broad exception handling.
- Do not introduce new NuGet packages when the BCL or existing dependencies are sufficient.
- Keep terminal I/O injectable through `TextReader` / `TextWriter` where practical so tests can exercise interactive flows.
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
- Prefer stubbed `IAssetsReader`, `IAssetsPatchWriter`, or `IAssetsFileService` tests for workflow and terminal behavior.
- Use real `AssetsToolsFileAccess` / `ScopedAssetsReader` tests only when the behavior depends on AssetsTools.NET integration.
- Before reporting code changes as complete, run `dotnet test UnityAssetsPatcher.sln` unless the change is documentation-only or the user asks not to run tests.

## Repository Hygiene

- Do not revert user changes or unrelated work.
- Build outputs under `bin/` and `obj/` are ignored and should not be committed.
- The current branch may be ahead of `origin/main`; do not rewrite history unless the user explicitly requests it.
