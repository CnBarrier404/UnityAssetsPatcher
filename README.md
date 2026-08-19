# Unity Assets Patcher

[Documentation](https://uap.cnbarrier.com) · [简体中文](README_ZH.md)

Unity Assets Patcher is a terminal application for inspecting Unity assets files and installing or uninstalling assets mods. It is intended for games where runtime mod frameworks such as `BepInEx` are impractical, using a `manifest.json` inside each mod package to describe file copies and assets modifications.

## Features

- An interactive terminal UI for inspecting assets files and installing or uninstalling zip-based mod packages.
- A non-interactive CLI with text and JSON output.
- Manifest validation with JSON Schema and semantic rules.
- Backups and install records for safe uninstallation and interrupted-operation recovery.
- Validation of mod ZIP entries, file paths, directory traversal, and unsafe file operations.

## Documentation

- [Get started](https://uap.cnbarrier.com)
- [Frequently asked questions](https://uap.cnbarrier.com/faq)
- [Mod manifest guide](https://uap.cnbarrier.com/mod-manifest-guide)

## Download

Download the latest Windows executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases). The current release supports `win-x64` and is distributed as a self-contained single file.

```powershell
.\UnityAssetsPatcher.exe
```

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
