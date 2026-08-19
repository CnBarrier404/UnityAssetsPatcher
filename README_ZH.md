# Unity Assets Patcher

[文档](https://uap.cnbarrier.com/zh-cn) · [English](README.md)

Unity Assets Patcher 是一款用于浏览 Unity assets 文件及安装、卸载 assets Mod 的终端工具。它适用于不便接入 `BepInEx` 等运行时 Mod 框架的游戏，通过 Mod 包中的 `manifest.json` 描述文件复制与 assets 修改。

## 功能

- 提供交互式终端界面，用于浏览 assets 文件以及安装和卸载 zip 格式的 Mod 包。
- 提供支持文本和 JSON 输出的非交互式 CLI。
- 使用 JSON Schema 和语义规则校验 manifest。
- 通过备份和安装记录支持安全卸载与中断操作恢复。
- 对 Mod ZIP 条目、文件路径、目录穿越和不安全文件操作进行校验。

## 文档

- [开始使用](https://uap.cnbarrier.com/zh-cn)
- [常见问题](https://uap.cnbarrier.com/zh-cn/faq)
- [Mod Manifest 编写指南](https://uap.cnbarrier.com/zh-cn/mod-manifest-guide)

## 下载

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载最新的 Windows 可执行文件。当前发布包支持 `win-x64`，采用自包含单文件形式。

```powershell
.\UnityAssetsPatcher.exe
```

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
