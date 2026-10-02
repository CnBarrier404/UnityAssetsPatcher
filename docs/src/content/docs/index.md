---
title: Get started
description: Download Unity Assets Patcher and install or manage mods in the desktop interface.
sidebar:
  order: 1
---

## Requirements

Windows builds support `win-x64`. They are distributed as a self-contained, single-file application, so you do not need to install the .NET runtime.

## Download

Download the executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases), then double-click it to open the desktop interface.

## Install a mod

1. Open **Install Mod**. Drag one `.zip` mod package into the window, or click **Select file**.
2. After the preview loads, review the mod's name, version, author, description, and target game directory. Click **Change** to select another game directory if needed.
3. Select any optional content you want to install. Each checkbox updates the installation preview; optional groups are initially unchecked.
4. Click **Start install**. The completion page shows the installed mod and any selected optional groups.

Loading a package and changing its preview do not modify game files. Click **Choose another** to return to package selection before installation, or **Install another mod** after it completes.

The initial preview uses the manifest's `game` field to locate a Steam installation. It must resolve one game directory and find the target assets files before the preview can open. See the [FAQ](/faq) if a package cannot be loaded.

## Manage installed mods

Open **Manage Mods** to see installed mods, their versions, target games, and installation times. Use **Refresh** to reload the list.

Click **Uninstall** beside a mod. The application checks file integrity and whether the remaining mods can be rebuilt, then asks for confirmation. A successful uninstall refreshes the list. If another mod depends on it, the notification identifies the affected mod.

## Settings and updates

In **Settings**, enable **Verbose logging** when troubleshooting or click **Open folder** to view the log directory. Verbose logging takes effect immediately for the current session and resets when the application restarts.

The application checks the latest stable GitHub release at startup. You can also click **Check for updates** in **Settings**. When a newer version is available, **Open release page** opens it in your browser so you can download the executable.

## Data and backups

Install records and layered backups are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\backup` by default. Logs are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\logs`, with up to five recent files retained.

Keep the backup directory available. It is required to uninstall mods safely.
