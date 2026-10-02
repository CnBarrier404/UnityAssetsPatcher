# Unity Assets Patcher

[Documentation](https://uap.cnbarrier.com) · [简体中文](README_ZH.md)

Unity Assets Patcher is a .NET 10 and Avalonia desktop application for installing and managing Unity assets mods. It is intended for games where runtime mod frameworks such as `BepInEx` are impractical, using a `manifest.json` inside each mod package to describe file copies and assets modifications.

## Features

- A desktop interface for installing ZIP mod packages by dragging a file into the window or selecting it with a file picker.
- Installation preview with mod metadata, the target game directory, and selectable optional content.
- Installed mod management with refresh, dependency checks, and uninstall confirmation.
- Manifest validation with JSON Schema and semantic rules.
- Layered backups and install records for safe uninstallation, with interrupted-transaction checks before changes.
- Validation of mod ZIP entries, file paths, directory traversal, and unsafe file operations.
- Startup and manual update checks, plus settings for verbose logging and opening the log directory.

## Documentation

- [Get started](https://uap.cnbarrier.com)
- [Frequently asked questions](https://uap.cnbarrier.com/faq)
- [Mod manifest guide](https://uap.cnbarrier.com/mod-manifest-guide)

## Download

Download the Windows executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases). Windows builds support `win-x64` and are distributed as a self-contained single file, with no separate .NET runtime required. Double-click the executable to open the desktop interface.

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
