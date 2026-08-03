# Unity Assets Patcher

[简体中文](README.md)

Unity Assets Patcher is a terminal application for inspecting Unity assets files and installing or uninstalling assets mods. It is intended for games where runtime mod frameworks such as `BepInEx` are impractical, using a `manifest.json` inside each mod package to describe file copies and assets modifications.

## Features

- An interactive terminal UI for inspecting assets files and installing or uninstalling zip-based mod packages.
- A non-interactive CLI for validating manifests, inspecting assets files, and previewing or applying mod installation, uninstallation, and recovery operations.
- Manifest validation with JSON Schema and semantic rules; supported CLI workflows provide text output or script-friendly JSON output.
- Backups before modifying files, with install records for safe uninstallation and interrupted-operation recovery.
- Validation of mod ZIP entries, file paths, directory traversal, and unsafe file operations.

## Download and Usage

Download the latest Windows executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases).

The current release supports `win-x64` only and is a self-contained single-file application with all required components, so a separate .NET runtime installation is not required.

Run the application without arguments to open the interactive UI:

```powershell
.\UnityAssetsPatcher.exe
```

Pass a command to use the non-interactive CLI. Run the help commands to see the currently supported commands and options:

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

Common CLI operations:

```powershell
# Validate a JSON manifest or mod ZIP package
.\UnityAssetsPatcher.exe check --config .\manifest.json
.\UnityAssetsPatcher.exe check --config .\Mod.zip

# Inspect an assets file
.\UnityAssetsPatcher.exe inspect list .\sharedassets0.assets --limit 100
.\UnityAssetsPatcher.exe inspect fields .\sharedassets0.assets 12345

# Preview and install a mod; apply requires an explicit --yes
.\UnityAssetsPatcher.exe install preview --package .\Mod.zip --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe install apply --package .\Mod.zip --game-directory "C:\Games\Game" --yes

# List install records, preview, and uninstall a mod
.\UnityAssetsPatcher.exe uninstall list
.\UnityAssetsPatcher.exe uninstall preview --id <install-id> --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe uninstall apply --id <install-id> --game-directory "C:\Games\Game" --yes

# Inspect and recover an interrupted install or uninstall
.\UnityAssetsPatcher.exe recovery preview --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe recovery apply --game-directory "C:\Games\Game" --yes
```

`install preview`, `uninstall preview`, and `recovery preview` do not modify game files. When `--game-directory` is omitted, `install` and `uninstall` try to resolve the game directory from Steam installation metadata; `recovery` requires an explicit game directory. Commands that support JSON output accept `--format Json`.

Installation records and backups are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\backup` by default. Logs are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\logs`, with up to five recent log files retained. Keep this directory available to uninstall mods or recover interrupted operations.

## Frequently Asked Questions

See the [FAQ (Chinese)](docs/faq.md).

## Creating Mods

A mod package is a zip archive containing exactly one `manifest.json`. The manifest can describe target assets files, field modifications, asset replacements, payload files, and optional content.

Every manifest must include a top-level `$schema` property set to [`https://uap.cnbarrier.com/schema-v1.json`](https://uap.cnbarrier.com/schema-v1.json) for editor completion and structural validation. Older manifests may temporarily retain `schemaVersion: 1`, but the current runtime does not read it; new manifests do not need to include that property.

Validate a manifest with the `check` command and run an installation preview before publishing a mod. See the [Mod Manifest Guide](docs/mod-manifest-guide.md) for the complete format, matching rules, and examples.

## Development and Contributing

Development requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Run the following commands from the repository root:

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet build UnityAssetsPatcher.slnx
dotnet test UnityAssetsPatcher.slnx
```

Project source code is located under `src/`, and test projects are located under `tests/`.

Issues and Pull Requests are welcome!

## License

This project is licensed under the [MIT License](LICENSE).

## Changelog

See the [CHANGELOG](CHANGELOG.md).

## Credits

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
