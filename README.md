# Unity Assets Patcher

[English](README_EN.md)

Unity Assets Patcher 是一款用于浏览 Unity assets 文件及安装、卸载 assets Mod 的终端工具。它适用于不便接入 `BepInEx` 等运行时 Mod 框架的游戏，通过 Mod 包中的 `manifest.json` 描述文件复制与 assets 修改。

## 功能

- 提供交互式终端界面，用于浏览 assets 文件以及安装和卸载 zip 格式的 Mod 包。
- 提供非交互式 CLI，用于验证 manifest、浏览 assets 文件，以及预览或执行 Mod 的安装与卸载。

## 下载与使用

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载最新版本的 Windows 可执行文件。

当前发布包仅支持 `win-x64`，并已包含运行所需组件，无需预先安装 .NET 运行时。

直接运行程序即可进入交互式界面：

```powershell
.\UnityAssetsPatcher.exe
```

传入命令即可使用非交互式 CLI。运行帮助命令可查看当前支持的命令与参数：

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

安装记录和备份默认保存在 `%LOCALAPPDATA%\UnityAssetsPatcher` 中。请保留该文件夹，以便正常卸载 Mod 或恢复意外中断的操作。

## Mod 创建

Mod 包采用 zip 格式，其中必须包含且只能包含一个 `manifest.json`。Manifest 可用于描述目标 assets 文件、字段修改、asset 替换、payload 文件及可选内容。

完整的格式说明、匹配规则与示例请参阅 [Mod Manifest 编写指南](docs/mod-manifest-guide.md)。

## 开发和贡献

开发环境需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。在仓库根目录运行：

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
dotnet test UnityAssetsPatcher.slnx
```

项目源码位于 `src/`，测试位于 `tests/UnityAssetsPatcher.Tests/`。

欢迎通过 Issue 反馈问题或提出建议，也欢迎提交 Pull Request！

## 许可证

本项目使用 [MIT License](LICENSE)。

## 致谢

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
