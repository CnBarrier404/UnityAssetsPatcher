---
title: Frequently asked questions (FAQ)
description: Common Unity Assets Patcher questions and solutions.
sidebar:
  order: 2
---

## Why does a mod package fail to load?

Select or drag in one `.zip` file containing exactly one `manifest.json`. The manifest must use the supported `$schema` URL, and its patches and payload files must satisfy the [manifest rules](/mod-manifest-guide).

The initial preview also needs the target game and assets files. Check that the manifest's `game` matches the name in Steam's installation information and resolves to one installed game. Each target assets file name must match exactly one file under that game directory.

If the notification does not explain the problem, enable **Verbose logging** in **Settings**, load the package again, and click **Open folder** to read the latest log.

## Why is uninstall blocked?

Before uninstalling, the application checks whether the game files still match the recorded state and whether the remaining mods can be rebuilt. External changes to game files, damaged backups, or a dependency in another mod can block the operation.

If the notification names dependent mods, uninstall those first. For other failures, check the latest log and keep the backup directory intact.

## Where are backups and logs stored?

Backups and install records are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\backup`. The `base/` directory holds base snapshots, and `layers/<install-id>/` holds each install record and original mod package. These files are required for uninstallation.

Logs are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\logs`, with up to five recent files retained. **Settings → Open folder** opens this directory. **Verbose logging** adds debug information for the current session; enable it again after restarting if needed.

## Why did the update check fail?

Update checks request the latest stable release from GitHub. Network failures, API rate limits, or invalid release data can cause a check to fail. Startup check failures are silent; manual checks in **Settings** show a notification.

Try again later or download the executable directly from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases). When a newer version is found, **Open release page** opens the download page in your browser.
