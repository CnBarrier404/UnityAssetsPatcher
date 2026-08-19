---
title: Get started
description: Download and run Unity Assets Patcher.
sidebar:
  order: 1
---

## Requirements

The current release supports `win-x64`. It is distributed as a self-contained, single-file application, so you do not need to install the .NET runtime.

## Download

Download the latest executable from [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases).

## Open the terminal UI

Run the application without arguments:

```powershell
.\UnityAssetsPatcher.exe
```

The interactive UI can inspect assets files and guide you through installing, uninstalling, and recovering mods. A modern terminal such as [Windows Terminal](https://aka.ms/terminal) is recommended.

## Use the CLI

Pass a command to use the non-interactive interface:

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

## Data and backups

Install records and layered backups are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\backup` by default. Logs are stored in `%LOCALAPPDATA%\UnityAssetsPatcher\logs`, with up to five recent files retained.

Keep the backup directory available. It is required to uninstall mods or recover interrupted operations safely.
