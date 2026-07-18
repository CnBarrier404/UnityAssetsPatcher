# Unity Assets Patcher

[English](README_EN.md)

Unity Assets Patcher 是一个用于安装和卸载 Unity `.assets` 文件 Mod 的交互式命令行工具。它面向不适合接入 `BepInEx` 等运行时 Mod 框架的 Unity 游戏，通过 Mod 包中的 `manifest.json` 描述要复制的文件和要写入的 assets 变更。

## 功能概览

- 安装 zip 格式的 Mod 包，并在写入前显示预览。
- 修改 Unity `.assets` 文件中的字段，或用 Mod 包内的 asset 替换目标 asset。
- 复制 Mod 所需的 payload 文件，例如 `.resource` 文件。
- 为被覆盖的 assets 文件创建备份，并记录安装信息。
- 卸载已安装的 Mod，恢复安装备份并清理安装时复制的 payload 文件。
- 支持英文和简体中文终端界面。
- 内置路径安全检查、manifest 大小限制和 zip 解压大小限制。

## 下载与运行

从 [GitHub Releases](https://github.com/CnBarrier404/UnityAssetsPatcher/releases) 下载最新的 Windows 压缩包，解压到一个单独文件夹，然后运行 `UnityAssetsPatcher.exe`。

当前发布包是 `win-x64` 自包含单文件程序，不需要预先安装 .NET 运行时。运行所需的 `resources.tpk` 已嵌入可执行文件；安装记录和备份默认保存在程序目录下的 `backup` 文件夹。

## 安装 Mod

1. 启动 `UnityAssetsPatcher.exe`。
2. 在主菜单选择 `Install Mod`。
3. 输入 Mod zip 文件路径。
4. 输入游戏目录，或留空让工具尝试通过 Steam 安装信息自动解析。
5. 阅读安装预览，确认目标 assets 文件、将修改的 asset、payload 文件和备份信息。
6. 只有在确认后，工具才会写入 assets 文件并复制 payload 文件。

## 卸载 Mod

1. 启动 `UnityAssetsPatcher.exe`。
2. 在主菜单选择 `Uninstall Mod`。
3. 从已安装 Mod 列表中选择要卸载的记录。
4. 阅读卸载预览，确认要恢复的 assets 文件和要删除的 payload 文件。
5. 确认后，工具会从安装时创建的备份恢复被修改的 assets 文件，并删除安装时复制的 payload 文件。

卸载依赖安装记录和备份文件。如果 `backup` 文件夹被删除或移动，卸载可能无法继续。多个 Mod 修改同一 assets 文件时，必须按安装顺序的反向顺序卸载；预览会列出需要优先卸载的后装 Mod 和冲突文件。修改不同 assets 文件的 Mod 不受此限制。

## 安全机制

- 安装和卸载都会先预览，预览阶段不会写入 assets 文件、复制 payload 文件、删除 payload 文件或更新安装记录。
- 覆盖原始 assets 文件前会先创建备份。
- 写入使用临时文件，失败时会清理临时文件。
- 写入字段前会校验 manifest 中声明的 `from` 值，避免在不匹配的游戏版本上继续修改。
- 显式输出路径不能指向输入文件，也不能覆盖已有输出文件。
- payload 文件不会覆盖已有文件。
- Mod 包路径会被校验，拒绝绝对路径、路径遍历和不安全路径。
- `manifest.json` 最大 10MB，Mod 包解压内容最大 10GB。

## Mod 包基础

Mod 包是一个 zip 文件，里面必须包含且只能包含一个 `manifest.json`。如果 Mod 需要安装额外文件，例如 `.resource` 文件，必须在 manifest 的 `copyFiles` 中显式声明。

```text
Mod.zip
  manifest.json
  resources/
    modassets.assets
    modassets.resource
```

最小概念：

- `targets[].file` 按文件名定位目标 `.assets` 文件。
- `patches[]` 用 Unity asset 类型和字段匹配目标 asset。
- `set` 修改字段值，并要求当前值匹配 `from`。
- `add` 向数组字段追加标量值。
- `replaceAsset` 用 Mod 包中的源 asset 替换目标 asset。
- `$pathId` 可用于把字段写成同一 assets 文件中另一个 asset 的 Path ID。

完整 manifest 格式、字段路径、匹配规则和示例见 [Mod Manifest 编写指南](docs/mod-manifest-guide.md)。

## 当前限制

- 当前发布包只提供 Windows `win-x64`。
- 这是交互式终端工具，目前没有图形界面和非交互式 CLI 参数。
- 自动游戏目录解析主要依赖 Steam 安装信息；无法唯一解析时需要手动输入游戏目录。
- `targets[].file` 只能写目标 assets 文件名，不能写目录路径；工具会在游戏目录下递归查找。
- payload 文件会复制到目标 assets 文件所在目录，且要求目标文件不存在。
- 同一个目标 assets 文件内不能混合整 asset 替换和字段级 `set` / `add` 修改。
- Unity assets 字段结构会随游戏版本、Unity 版本和导出方式变化；manifest 作者应使用 UABEA 等工具确认字段路径和旧值。

## 常见问题

**找不到游戏目录**

如果自动解析失败，请手动输入游戏安装目录。该目录应包含游戏数据文件夹，工具会在其中递归查找目标 `.assets` 文件。

**预览显示 skipped**

通常表示当前字段值与 manifest 中的 `from` 值不一致。请确认游戏版本、Mod 版本和 manifest 是否匹配。

**卸载失败**

卸载需要完整的安装记录、安装备份和当前目标文件。记录包含格式版本、由规范化游戏目录生成的实例指纹和该实例的安装序号；旧格式记录不会自动迁移。如果安装后的 `backup` 文件夹被删除、游戏目录被移动，或同一 assets 文件仍有后装 Mod，卸载会被拒绝。如果某个 assets 文件恢复失败且前面的文件已经恢复，工具会使用临时回滚备份将前面的文件恢复到卸载前状态。

**payload 文件已存在**

安装不会覆盖已有 payload 文件。请先确认该文件是否来自旧 Mod 或手动安装内容，再决定是否清理。

## 开发者入口

开发环境需要安装 `.NET 10 SDK`。从仓库根目录运行：

```powershell
dotnet run --project src\UnityAssetsPatcher\UnityAssetsPatcher.csproj
```

```powershell
dotnet test UnityAssetsPatcher.sln
```

项目结构：

- `src/UnityAssetsPatcher`：可执行程序入口、依赖注入组合和 bundled resource 设置。
- `src/UnityAssetsPatcher.TUI`：交互式终端界面、页面、提示和输出格式。
- `src/UnityAssetsPatcher.Application`：assets contracts、字段模型、manifest 加载、安装/卸载工作流、patch 规划和业务流程。
- `src/UnityAssetsPatcher.AssetsTools`：AssetsTools.NET 集成和真实 Unity assets 文件读写。
- `tests/UnityAssetsPatcher.Tests`：xUnit v3 测试。

发布构建配置在 [`.github/workflows/release.yml`](.github/workflows/release.yml)。工作流会通过 `dotnet publish` 传入 `-p:PublishAot=true`，将 `win-x64` 发布为自包含、单文件 NativeAOT 程序。

## 相关文档

- [Mod Manifest 编写指南](docs/mod-manifest-guide.md)
- [变更记录](docs/changelog.md)

## 许可证

本项目使用 [MIT License](LICENSE)。

## 致谢

- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [AssetsRipper TPK](https://github.com/AssetRipper/Tpk)
