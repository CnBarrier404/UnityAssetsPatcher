# AGENTS.md

This file applies to the entire repository.

## Project Overview

UnityAssetsPatcher is a .NET 10 command-line tool for inspecting, querying, and patching Unity assets files. It is not a Unity project; do not use Unity Editor workflows for normal development.

The solution contains:

- `src/UnityAssetsPatcher`: CLI application and production code.
- `tests/UnityAssetsPatcher.Tests`: xUnit v3 test project.
- `src/UnityAssetsPatcher/Assets/AssetsRipper.tpk`: type package copied beside the executable as `resources.tpk`.

## Common Commands

Use these from the repository root:

```powershell
dotnet test UnityAssetsPatcher.sln
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- --help
```

For targeted CLI checks:

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- inspect list <assets-file>
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- find <assets-file> --config <manifest.json>
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj -- patch preview <assets-file> --config <manifest.json>
```

## Architecture Notes

- `Program.cs` wires the app and resolves `resources.tpk` from `AppContext.BaseDirectory`; avoid relying on the process working directory for bundled resources.
- `ConsoleApp` builds the command tree and owns top-level exception handling.
- CLI commands live in `src/UnityAssetsPatcher/Cli` and should implement `ICommandModule`; register new commands through `CommandCatalog`.
- Domain workflow logic lives in `AssetsWorkflowService`.
- Keep AssetsTools.NET-specific behavior inside `src/UnityAssetsPatcher/AssetsTools`.
- Keep external access behind `IAssetsReader` and `IAssetsPatchWriter` so workflow code remains testable with stubs.
- Manifest parsing belongs in `AssetQueryConfigLoader`; field path/value matching belongs in `AssetFieldMatcher`.

## Coding Guidelines

- Keep the SDK-style project format and `net10.0` target unless the user explicitly asks to change them.
- Preserve nullable correctness and file-scoped namespaces.
- Prefer `System.Text.Json` for JSON work.
- Prefer clear guard clauses and explicit error messages over broad exception handling.
- Keep CLI output stable unless the requested change is about user-facing output.
- Do not introduce new NuGet packages when the BCL or existing dependencies are sufficient.

## Patch Safety

Patch writing can modify real Unity assets files, so keep these safeguards intact:

- Do not allow `--output` to point at the input file.
- Do not overwrite an existing explicit output file.
- When overwriting the input path, create a backup first.
- Write through a temporary file and clean it up on failure.
- Release `AssetsManager` resources before replacing files.
- Validate current field values against manifest `from` values before writing `to` values.

## Testing Expectations

- Add or update focused tests in `tests/UnityAssetsPatcher.Tests` for behavior changes.
- Prefer stubbed `IAssetsReader` / `IAssetsPatchWriter` tests for workflow and CLI behavior.
- Use real `AssetsToolsReader` tests only when the behavior depends on AssetsTools.NET integration.
- Before reporting code changes as complete, run `dotnet test UnityAssetsPatcher.sln` unless the change is documentation-only or the user asks not to run tests.

## Repository Hygiene

- Do not revert user changes or unrelated work.
- Build outputs under `bin/` and `obj/` are ignored and should not be committed.
- The current branch may be ahead of `origin/main`; do not rewrite history unless the user explicitly requests it.
