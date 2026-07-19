# Unity Assets Patcher

[简体中文](README.md)

Unity Assets Patcher is an interactive command-line tool for installing and uninstalling Unity `.assets` file mods. It is designed for Unity games where a runtime mod framework such as `BepInEx` is not practical, and it uses a `manifest.json` inside each mod package to describe copied files and assets changes.

## What It Does

- Installs zip-based mod packages with a preview before writing files.
- Patches fields inside Unity `.assets` files, or replaces target assets from assets bundled in the mod package.
- Copies payload files required by the mod, such as `.resource` files.
- Creates backups for overwritten assets files and records each installation.
- Uninstalls installed mods by restoring install backups and deleting copied payload files.
- Supports English and Simplified Chinese terminal UI.
- Includes path safety checks, manifest size limits, and zip extraction limits.

## Download and Run

Download the latest Windows EXE from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) and run it directly.

The current release package is a self-contained `win-x64` single-file executable, so the .NET runtime does not need to be installed first. The required `resources.tpk` is embedded in the executable, and install records plus backups are stored in the `backup` folder next to the program by default.

## Install a Mod

1. Start `UnityAssetsPatcher.exe`.
2. Choose `Install Mod` from the main menu.
3. Enter the path to the mod zip file.
4. Enter the game directory, or leave it blank so the tool can try to resolve the game through Steam install metadata.
5. Review the install preview, including target assets files, matched assets, payload files, and backup information.
6. Files are written only after you confirm the preview.

## Uninstall a Mod

1. Start `UnityAssetsPatcher.exe`.
2. Choose `Uninstall Mod` from the main menu.
3. Select the installation record to uninstall.
4. Review the uninstall preview, including assets files to restore and payload files to delete.
5. After confirmation, the tool restores patched assets from the backups created during installation and deletes copied payload files.

Uninstall depends on the installation record and backup files. If the `backup` folder is deleted or moved, uninstall may no longer be possible.

## Safety Model

- Install and uninstall both start with a preview. Preview does not write assets files, copy payload files, delete payload files, or update install records.
- A backup is created before overwriting an original assets file.
- Writes go through temporary files, and temporary files are cleaned up on failure.
- Field writes validate the `from` value declared by the manifest before writing the `to` value, preventing mismatched game versions from being patched silently.
- Explicit output paths cannot point to the input file and cannot overwrite an existing output file.
- Payload files do not overwrite existing files.
- Mod package paths are validated to reject absolute paths, path traversal, and unsafe paths.
- `manifest.json` is capped at 10MB, and extracted mod package contents are capped at 10GB.

## Mod Package Basics

A mod package is a zip file that must contain exactly one `manifest.json`. If the mod needs extra files installed, such as `.resource` files, those files must be declared explicitly in the manifest `copyFiles` section.

```text
Mod.zip
  manifest.json
  resources/
    modassets.assets
    modassets.resource
```

Core concepts:

- `targets[].file` locates the target `.assets` file by file name.
- `patches[]` matches target assets by Unity asset type and field values.
- `set` changes field values and requires the current value to match `from`.
- `add` appends scalar values to array fields.
- `replaceAsset` replaces a target asset with a source asset from the mod package.
- `$pathId` can write the Path ID of another asset in the same assets file.

For the complete manifest format, field paths, matching rules, and examples, see the [Mod Manifest Guide](docs/mod-manifest-guide.md).

## Known Limitations

- Current release packages are Windows `win-x64` only.
- This is an interactive terminal tool. It currently has no graphical UI and no non-interactive CLI arguments.
- Automatic game directory resolution mainly depends on Steam install metadata. If resolution is not unique, the game directory must be entered manually.
- `targets[].file` accepts only the target assets file name, not a directory path. The tool searches recursively under the game directory.
- Payload files are copied next to the target assets files and require the destination files to be absent.
- A single target assets file cannot mix full asset replacement with field-level `set` / `add` patches.
- Unity assets field structures can change across game versions, Unity versions, and export methods. Manifest authors should verify field paths and old values with tools such as UABEA.

## Troubleshooting

**Game directory cannot be found**

If automatic resolution fails, enter the game install directory manually. The directory should contain the game data folder, and the tool will recursively search it for target `.assets` files.

**Preview shows skipped changes**

This usually means the current field value does not match the manifest `from` value. Check that the game version, mod version, and manifest match.

**Uninstall fails**

Uninstall requires the install record, install backup, and current target file to exist. If the `backup` folder was deleted after installation, or game files were moved manually, uninstall may be refused. If one assets restore fails after earlier files were restored, the tool uses temporary rollback backups to return those earlier files to their pre-uninstall state.

**Payload file already exists**

Install does not overwrite existing payload files. Check whether the file came from an older mod or manual installation before removing it.

## For Developers

Development requires the `.NET 10 SDK`. Run these commands from the repository root:

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

```powershell
dotnet test UnityAssetsPatcher.slnx
```

Project layout:

- `src/UnityAssetsPatcher`: executable entry point, dependency injection composition, and bundled resource setup.
- `src/UnityAssetsPatcher.TUI`: interactive terminal UI, pages, prompts, and output formatting.
- `src/UnityAssetsPatcher.Core`: shared assets contracts, field models, field-path matching, and utilities.
- `src/UnityAssetsPatcher.Application`: manifest loading, install/uninstall workflows, patch planning, and application flow.
- `src/UnityAssetsPatcher.AssetsTools`: AssetsTools.NET integration and real Unity assets file read/write behavior.
- `tests/UnityAssetsPatcher.Tests`: xUnit v3 tests.

Release builds are configured in [`.github/workflows/release.yml`](.github/workflows/release.yml). The workflow publishes `win-x64` as self-contained, single-file NativeAOT by passing `-p:PublishAot=true` to `dotnet publish`.

## Related Docs

- [Mod Manifest Guide](docs/mod-manifest-guide.md)
- [Changelog](docs/changelog.md)

## License

This project is licensed under the [MIT License](LICENSE).

## Credits

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
