# Unity Assets Patcher

[简体中文](README.md)

Unity Assets Patcher is a terminal application for inspecting Unity assets files and installing or uninstalling assets mods. It is intended for games where runtime mod frameworks such as `BepInEx` are impractical, using a `manifest.json` inside each mod package to describe file copies and assets modifications.

## Features

- An interactive terminal UI for inspecting assets files and installing or uninstalling zip-based mod packages.
- A non-interactive CLI for validating manifests, inspecting assets files, and previewing or applying mod installations and uninstallations.

## Download and Usage

Download the latest Windows executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases).

The current release supports `win-x64` only and includes all required components, so a separate .NET runtime installation is not required.

Run the application without arguments to open the interactive UI:

```powershell
.\UnityAssetsPatcher.exe
```

Pass a command to use the non-interactive CLI. Run the help commands to see the currently supported commands and options:

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

Installation records and backups are stored in `%LOCALAPPDATA%\UnityAssetsPatcher` by default. Keep this folder available to uninstall mods or recover interrupted operations.

## Frequently Asked Questions

See the [FAQ](docs/faq_EN.md).

## Creating Mods

A mod package is a zip archive containing exactly one `manifest.json`. The manifest can describe target assets files, field modifications, asset replacements, payload files, and optional content.

See the [Mod Manifest Guide](docs/mod-manifest-guide.md) for the complete format, matching rules, and examples.

## Development and Contributing

Development requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Run the following commands from the repository root:

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet test UnityAssetsPatcher.slnx
```

Project source code is located under `src/`, and tests are located under `tests/UnityAssetsPatcher.Tests/`.

Issues and Pull Requests are welcome!

## License

This project is licensed under the [MIT License](LICENSE).

## Credits

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
