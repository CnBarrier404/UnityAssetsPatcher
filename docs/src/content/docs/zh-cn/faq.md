---
title: 常见问题
description: Unity Assets Patcher 常见问题及解决方法。
sidebar:
  order: 2
---

## Q1: 为什么 Mod 包加载失败？

请选择或拖入一个 `.zip` 文件，包内必须只有一个 `manifest.json`。manifest 需要使用受支持的 `$schema` 地址，补丁和 payload 文件也必须符合 [manifest 规则](/zh-cn/mod-manifest-guide)。

首次预览还需要找到目标游戏和 assets 文件。请确认 manifest 的 `game` 与 Steam 安装信息中的游戏名称一致，且只能定位到一个已安装游戏。每个目标 assets 文件名也必须在该游戏目录下唯一匹配。

如果提示不足以确定原因，可以在**设置**中开启**详细日志**，重新加载 Mod 包，再点击**打开文件夹**查看最新日志。

## Q2: 为什么无法卸载 Mod？

卸载前，程序会检查游戏文件是否仍与安装记录一致，以及剩余 Mod 能否重新合成。游戏文件被其他程序修改、备份损坏或其他 Mod 存在依赖，都可能阻止卸载。

如果提示列出了依赖该 Mod 的其他 Mod，请先卸载这些 Mod。其他错误可以通过最新日志排查，并保留完整的备份目录。

## Q3: 备份和日志保存在哪里？

备份和安装记录保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\backup`。`base/` 保存基础快照，`layers/<install-id>/` 保存每次安装的记录和原始 Mod 包；卸载需要这些文件完整可用。

日志保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\logs`，最多保留最近五个文件。在**设置**中点击**打开文件夹**即可打开该目录。**详细日志**会为本次运行增加调试信息，重启后如有需要，请重新开启。

## Q4: 为什么检查更新失败？

检查更新会从 GitHub 获取最新稳定版本。网络故障、API 请求限额或无效的发布数据都可能导致检查失败。启动检查失败时不会弹出提示，设置中的手动检查会显示通知。

可以稍后重试，或直接从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载可执行文件。发现新版本后，点击**前往发布页**会在浏览器中打开下载页面。
