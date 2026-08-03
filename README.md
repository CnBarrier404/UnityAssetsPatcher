# Unity Assets Patcher

[English](README_EN.md)

Unity Assets Patcher 是一款用于浏览 Unity assets 文件及安装、卸载 assets Mod 的终端工具。它适用于不便接入 `BepInEx` 等运行时 Mod 框架的游戏，通过 Mod 包中的 `manifest.json` 描述文件复制与 assets 修改。

## 功能

- 提供交互式终端界面，用于浏览 assets 文件以及安装和卸载 zip 格式的 Mod 包。
- 提供非交互式 CLI，用于校验 manifest、浏览 assets 文件，以及预览或执行 Mod 的安装、卸载和中断操作恢复。
- 使用 JSON Schema 和语义规则校验 manifest；支持的 CLI 工作流可提供文本输出或脚本使用的 JSON 输出。
- 在修改文件前创建备份，并通过安装记录支持安全卸载和中断操作恢复。
- 对 Mod ZIP 条目、文件路径、目录穿越和不安全文件操作进行校验。

## 下载与使用

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载最新版本的 Windows 可执行文件。

当前发布包仅支持 `win-x64`，是包含运行所需组件的自包含单文件程序，无需预先安装 .NET 运行时。

直接运行程序即可进入交互式界面：

```powershell
.\UnityAssetsPatcher.exe
```

传入命令即可使用非交互式 CLI。运行帮助命令可查看当前支持的命令与参数：

```powershell
.\UnityAssetsPatcher.exe --help
.\UnityAssetsPatcher.exe install --help
```

常用 CLI 操作示例：

```powershell
# 检查 JSON manifest 或 ZIP Mod 包
.\UnityAssetsPatcher.exe check --config .\manifest.json
.\UnityAssetsPatcher.exe check --config .\Mod.zip

# 浏览 assets 文件
.\UnityAssetsPatcher.exe inspect list .\sharedassets0.assets --limit 100
.\UnityAssetsPatcher.exe inspect fields .\sharedassets0.assets 12345

# 预览并安装 Mod；apply 必须显式传入 --yes
.\UnityAssetsPatcher.exe install preview --package .\Mod.zip --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe install apply --package .\Mod.zip --game-directory "C:\Games\Game" --yes

# 查看安装记录、预览并卸载 Mod
.\UnityAssetsPatcher.exe uninstall list
.\UnityAssetsPatcher.exe uninstall preview --id <install-id> --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe uninstall apply --id <install-id> --game-directory "C:\Games\Game" --yes

# 检查并恢复中断的安装或卸载
.\UnityAssetsPatcher.exe recovery preview --game-directory "C:\Games\Game"
.\UnityAssetsPatcher.exe recovery apply --game-directory "C:\Games\Game" --yes
```

`install preview`、`uninstall preview` 和 `recovery preview` 不会修改游戏文件。`install` 和 `uninstall` 在省略 `--game-directory` 时会尝试通过 Steam 安装信息解析游戏目录；`recovery` 必须显式指定游戏目录。支持 JSON 输出的命令可以传入 `--format Json`。

安装记录和备份默认保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\backup`，运行日志保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\logs`，最多保留最近 5 个日志文件。请保留该目录，以便正常卸载 Mod 或恢复意外中断的操作。

## 常见问题

请参阅 [常见问题](docs/faq.md)。

## Mod 创建

Mod 包采用 zip 格式，其中必须包含且只能包含一个 `manifest.json`。Manifest 可用于描述目标 assets 文件、字段修改、asset 替换、payload 文件及可选内容。

每个 manifest 都必须在顶层包含 `$schema` 字段，并将其设置为 [`https://uap.cnbarrier.com/schema-v1.json`](https://uap.cnbarrier.com/schema-v1.json)，以启用编辑器补全和结构校验。旧 manifest 中的 `schemaVersion: 1` 可以暂时保留，但当前运行时不会读取它；新 manifest 不需要再添加该字段。

发布 Mod 前请使用 `check` 命令校验 manifest，并执行安装预览确认匹配范围。完整的格式说明、匹配规则与示例请参阅 [Mod Manifest 编写指南](docs/mod-manifest-guide.md)。

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
