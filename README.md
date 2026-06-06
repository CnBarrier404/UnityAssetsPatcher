# Unity Assets Patcher

Unity Assets Patcher 是一个用于预览、检查和安装 Unity `.assets` 文件 mod 的交互式命令行工具。

它主要面向无法方便接入 `BepInEx` 等运行时 mod 框架的 Unity 游戏，通过 manifest 描述要安装的资源文件和 assets 变更。

## 快速开始

从 Release 下载 Windows 压缩包，解压后双击运行 `UnityAssetsPatcher.exe`。

开发环境需要安装 `.NET 10 SDK`。从仓库根目录运行：

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

## 功能

- 安装 mod zip 包，并在写入前显示 dry run 预览。
- 检查 Unity assets 文件中的资产列表和字段树。
- 使用 manifest 条件查找目标 asset。
- 安装 manifest 中声明的 assets 变更和 payload 文件。
- 覆盖原始 assets 文件前自动创建备份。

## Mod 包与 Manifest

mod 包是一个 zip 文件，内部必须包含且只能包含一个 `manifest.json`。包内也可以包含需要安装到游戏目录的资源文件。

示例结构：

```text
Mod.zip
  manifest.json
  resources/
    modassets.assets
    modassets.resource
```

manifest 的字段、匹配规则和变更规则写法见 [Mod Manifest 编写指南](docs/mod-manifest-guide.md)。

## 使用注意事项

- 安装前会先显示 dry run 预览，确认后才会写入文件。
- 覆盖原始 assets 文件前会在程序目录的 `backup` 文件夹创建备份。
- 工具会校验 manifest 中声明的旧值，避免在不匹配的游戏版本上继续写入。
- 自动游戏目录解析主要依赖 Steam；无法唯一解析时需要手动选择游戏目录。
- payload 文件会复制到目标 assets 文件所在目录，且不会覆盖已有文件。
- 同一个目标 assets 文件内不能混合整 asset 替换和字段级修改。

## 开发

开发环境需要 `.NET 10 SDK`。

运行测试：

```powershell
dotnet test UnityAssetsPatcher.sln
```

运行应用：

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

项目使用 `Spectre.Console` 构建交互式终端界面，并通过 `AssetsTools.NET` 读写 Unity assets 文件。

## 相关文档

- [Mod Manifest 编写指南](docs/mod-manifest-guide.md)
- [变更记录](docs/changelog.md)

## 感谢

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper](https://github.com/AssetRipper/Tpk)