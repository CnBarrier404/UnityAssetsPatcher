# Unity Assets Patcher

[文档](https://uap.cnbarrier.com/zh-cn) · [English](README.md)

Unity Assets Patcher 是一款基于 .NET 10 和 Avalonia 的桌面应用，用于安装和管理 Unity assets Mod。它适用于不便接入 `BepInEx` 等运行时 Mod 框架的游戏，通过 Mod 包中的 `manifest.json` 描述文件复制与 assets 修改。

## 功能

- 提供桌面界面，可通过拖放文件或文件选择器安装 ZIP Mod 包。
- 安装预览展示 Mod 信息、目标游戏目录，并支持勾选可选内容。
- 管理已安装的 Mod，支持刷新列表、依赖检查和卸载确认。
- 使用 JSON Schema 和语义规则校验 manifest。
- 通过分层备份和安装记录支持安全卸载，并在变更前检查中断事务。
- 对 Mod ZIP 条目、文件路径、目录穿越和不安全文件操作进行校验。
- 支持启动时和手动检查更新，可在设置中切换详细日志并打开日志目录。

## 文档

- [开始使用](https://uap.cnbarrier.com/zh-cn)
- [常见问题](https://uap.cnbarrier.com/zh-cn/faq)
- [Mod Manifest 编写指南](https://uap.cnbarrier.com/zh-cn/mod-manifest-guide)

## 下载

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载 Windows 可执行文件。Windows 构建支持 `win-x64`，采用自包含单文件形式，无需另外安装 .NET 运行时。双击可执行文件即可打开桌面界面。

## 开发和贡献

开发环境需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。在仓库根目录运行：

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet build UnityAssetsPatcher.slnx
dotnet test UnityAssetsPatcher.slnx
```

项目源码位于 `src/`，测试项目位于 `tests/`。

欢迎通过 Issue 反馈问题或提出建议，也欢迎提交 Pull Request！

## 许可证

本项目使用 [MIT License](LICENSE)。

## 变更记录

请参阅 [CHANGELOG](CHANGELOG.md)。

## 致谢

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
