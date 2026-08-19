---
title: 开始使用
description: 下载并运行 Unity Assets Patcher。
sidebar:
  order: 1
---

## 系统要求

当前发布包支持 `win-x64`，采用包含运行组件的自包含单文件形式，无需另外安装 .NET 运行时。

## 下载

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载最新版本。

## 打开交互式界面

直接运行程序：

```powershell
.\UnityAssetsPatcher.exe
```

交互式界面可用于浏览 assets 文件，并引导完成 Mod 安装、卸载和恢复。建议使用 [Windows Terminal](https://aka.ms/terminal) 等现代终端。

## 使用 CLI

传入命令即可使用非交互式界面：

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

## 数据与备份

安装记录和分层备份默认保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\backup`。日志保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\logs`，最多保留最近五个文件。

请保留备份目录，以便安全卸载 Mod 或恢复中断的操作。
