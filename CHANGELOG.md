# Changelog

## v0.5.1

本次版本为旧版 Windows 控制台提供临时兼容支持，并引导用户切换到 Windows Terminal，以改善交互界面的显示效果。

### 修复

- Windows 下的交互界面现在使用专用终端驱动，改善旧版 Windows 控制台中的兼容性
- 旧版控制台会使用明确的深色前景色和背景色，减少边框、文字和界面布局显示异常
- Windows Terminal 等现代终端会继续沿用终端配置的背景色，不再被兼容配色覆盖

### 改进

- 检测到旧版 Windows 控制台时，界面会显示警告并建议切换到 Windows Terminal
- 常见问题文档新增 Windows Terminal 的安装、默认终端切换和程序重启说明

---

This release adds temporary compatibility support for the legacy Windows console and guides users toward Windows Terminal for better interactive UI rendering.

### Fixed

- The interactive UI now uses the dedicated Windows terminal driver on Windows, improving compatibility with the legacy Windows console
- Legacy consoles now use explicit dark foreground and background colors to reduce rendering issues with borders, text, and layouts
- Modern terminals such as Windows Terminal continue to inherit their configured background colors instead of having them overridden by compatibility colors

### Improved

- The UI now warns when the legacy Windows console is detected and recommends switching to Windows Terminal
- Added FAQ instructions for installing Windows Terminal, making it the default terminal, and restarting the application

## v0.5.0

本次版本进一步提升了安装和卸载的可靠性，并为 Mod 作者新增同文件 asset 复制能力。

### 新增

- Manifest 新增 `copyAsset` 操作，可将同一 `.assets` 文件中的 asset 复制到另一个同类型 asset
- `add` 现在支持向空数组添加标量值
- CLI 和 TUI 新增中断操作恢复预览，只有在用户确认游戏目录后才会执行恢复

### 修复

- 修复浮点字段因微小精度差异而无法匹配的问题
- 修复部分情况下 Mod 临时文件被占用而无法清理的问题
- 修复更新检查可能受到 GitHub API 限流的问题
- 调整卸载页面的时间和按钮布局，并修复部分空格键操作异常

### 改进

- 安装、卸载和中断恢复现在更加安全，遇到无法确认的文件状态时不会继续覆盖或删除文件
- 优化大型 Assets 文件的处理速度和内存占用
- Release 下载由 ZIP 改为可直接运行的 EXE，并提供 SHA-256 校验值

### 升级说明

- 安装记录和备份现在保存在 `%LOCALAPPDATA%\UnityAssetsPatcher\backup`。旧版本位于程序目录的备份不会自动迁移，升级前请先使用旧版本卸载已安装的 Mod（这个版本以及之前，根本就没人用，对吧）

---

This release further improves install and uninstall reliability and adds same-file asset copying for mod authors.

### Added

- Added the manifest `copyAsset` operation for copying an asset to another asset of the same type within one `.assets` file
- `add` can now append scalar values to empty arrays
- Added interrupted-operation recovery previews to the CLI and TUI; recovery runs only after the user confirms the game directory

### Fixed

- Fixed floating-point fields failing to match because of minor precision differences
- Fixed mod temporary files remaining locked in some cases
- Fixed update checks being affected by GitHub API rate limits
- Adjusted uninstall timestamp and button layouts and fixed several unexpected space-key actions

### Improved

- Installs, uninstalls, and interrupted-operation recovery are now safer and will not overwrite or delete files when their state cannot be verified
- Improved processing speed and memory usage for large assets files
- Release downloads have changed from ZIP archives to directly runnable EXE files with SHA-256 checksums

### Upgrade Notes

- Install records and backups are now stored under `%LOCALAPPDATA%\UnityAssetsPatcher\backup`. Backups beside the executable from older versions are not migrated, so uninstall existing mods with the previous version before upgrading

## v0.4.0

TUI 全面重构！交互界面已完整迁移到 Terminal.Gui，在导航、操作反馈、长内容展示和后台任务处理等方面提供了更好、更一致且更稳定的使用体验。同时新增完整的非交互式 CLI 和 Assets 文件浏览能力，进一步完善手动操作与自动化场景。

### 新增

- 新增非交互式 CLI：可通过命令验证 manifest、浏览 Assets 文件，以及预览或执行 Mod 安装与卸载；不带参数启动时仍会进入交互界面
- CLI 支持文本和 JSON 输出，并使用明确的成功、操作失败和参数错误退出码，便于脚本和 CI 集成
- 安装与卸载 CLI 将预览和执行拆分为独立命令；实际修改文件时必须显式传入确认参数，预览操作不会改动文件
- 新增 Assets 文件浏览功能，可列出 Path ID、类型和名称，并查看指定资产的完整字段树；CLI 和交互界面均可使用
- Manifest 可通过独立命令直接验证，支持 JSON 文件、Mod ZIP 包以及默认的当前目录 `manifest.json`

### 修复

- 耗时工作流现在会在后台执行并将结果安全地送回界面，避免安装、卸载和 Assets 读取期间界面失去响应
- 动态工作流页面现在支持滚动，较长的预览、结果、资产列表和字段树不会被终端窗口截断
- 本地化源生成器移除了对 `System.Text.Json` 的运行时依赖，避免生成器加载环境缺少依赖时构建失败

### 改进

- TUI 已从旧版 Spectre.Console 页面全面重构为持久化 Terminal.Gui 界面；主菜单、安装、卸载、设置和 Assets 浏览现在使用统一的导航、表单、表格、按钮与快捷键，带来更流畅、更一致且更稳定的交互体验
- 可选内容选择器现在更适合键盘操作，并提供明确的提交动作；路径和数值输入会即时验证和规范化
- 交互界面会显示即时进度，并阻止多个修改操作重叠执行；终端标题也已精简，便于在窗口和标签页中识别
- 安装、卸载、恢复、manifest 验证和 Assets 浏览统一通过应用层工作流门面调用，CLI 与交互界面的行为更加一致
- AssetsTools 类型数据库 `resources.tpk` 现在嵌入可执行文件，发布包不再需要在程序旁携带该文件

### 破坏性变更

- Manifest 现在必须包含整数 `schemaVersion: 1`；缺失、非整数或不受支持的版本都会被拒绝，旧 Mod 包需要补充该字段
- Manifest patch 的 `component` 属性已重命名为 `componentType`；旧属性会被明确拒绝，需要更新现有 manifest
- 安装与卸载应用层契约现在使用稳定的安装 ID 标识记录，不再以备份目录作为主要标识；依赖这些公共契约的外部集成需要更新

---

Complete TUI overhaul! The interactive interface has been fully migrated to Terminal.Gui, delivering a better, more consistent, and more reliable experience through improved navigation, operation feedback, long-content presentation, and background workflow handling. It also adds a complete non-interactive CLI and assets file browsing to expand both manual and automated workflows.

### Added

- Added a non-interactive CLI for validating manifests, browsing assets files, and previewing or applying mod installs and uninstalls; launching without arguments still opens the interactive interface
- CLI commands support text and JSON output with distinct exit codes for success, operation failures, and usage errors, making them suitable for scripts and CI
- Install and uninstall CLI workflows separate preview from apply commands; mutating operations require an explicit confirmation option, while previews never modify files
- Added assets file browsing in both the CLI and interactive interface, including Path IDs, types, names, and the complete field tree for a selected asset
- Manifests can now be validated directly from a JSON file, a mod ZIP package, or the default `manifest.json` in the current directory

### Fixed

- Long-running workflows now execute in the background and dispatch results safely back to the interface, keeping the UI responsive during installs, uninstalls, and assets reads
- Dynamic workflow pages are now scrollable, preventing long previews, results, asset lists, and field trees from being clipped by the terminal window
- The localization source generator no longer has a runtime dependency on `System.Text.Json`, preventing build failures when that dependency is unavailable in the generator load context

### Improved

- The TUI has been completely rebuilt from the legacy Spectre.Console pages as a persistent Terminal.Gui interface; the main menu, install, uninstall, settings, and assets browsing views now share consistent navigation, forms, tables, buttons, and shortcuts for a smoother and more reliable experience
- Optional content selection is now more keyboard-friendly and has an explicit submit action; path and numeric inputs provide immediate validation and normalization
- The interface now shows immediate progress and prevents overlapping mutating operations; its compact terminal title is also easier to identify in windows and tabs
- Install, uninstall, recovery, manifest validation, and assets browsing now share the application workflow facade, keeping CLI and interactive behavior aligned
- The AssetsTools type database, `resources.tpk`, is now embedded in the executable, so the release package no longer needs that file beside the program

### Breaking Changes

- Manifests must now contain the integer property `schemaVersion: 1`; missing, non-integer, and unsupported versions are rejected, so existing mod packages need to add this field
- The manifest patch property `component` has been renamed to `componentType`; the legacy property is explicitly rejected and existing manifests must be updated
- Install and uninstall application contracts now identify records by stable install ID instead of using the backup directory as the primary identifier; external integrations using these public contracts must be updated

## v0.3.0

本次版本重点增强安装与卸载安全性，可在操作中断后自动恢复，并更严格地保护游戏文件和 Mod 安装层级。

### 新增

- 启动时会检查 GitHub 最新版本；网络或响应异常不会影响程序正常使用
- 安装和卸载操作现在使用事务日志记录进度，程序重启后会自动恢复未完成的操作；无法安全恢复的记录会被隔离
- 卸载预览会显示与当前 Mod 冲突且需要优先卸载的后装 Mod，以及发生重叠的 assets 文件
- 安装记录现在保存游戏实例指纹、安装序号，以及 assets、备份和 payload 文件的长度与 SHA-256 完整性信息

### 修复

- 多个 Mod 修改同一 assets 文件时，现在强制按安装顺序的反向顺序卸载，避免恢复旧备份覆盖后装 Mod 的修改
- 卸载前会验证当前 assets、安装备份和 payload 文件的完整性；已修改或无法读取的文件会阻止不安全的覆盖或删除
- 卸载多个 assets 文件时，如果后续恢复失败，已经恢复的文件会回滚到卸载前状态
- 安装目标与卸载记录路径会拒绝绝对路径、目录穿越和重解析点逃逸，避免读写到可信目录之外
- Assets 字段验证会保留整数和浮点数等标量的实际类型，避免字符串转换造成错误匹配或精度问题
- Assets 读取会话现在可以在写入前安全关闭并按需重新打开，避免残留文件句柄阻止替换文件

### 改进

- 安装和卸载操作通过共享锁串行执行，避免并发修改备份记录或游戏文件
- 卸载路径会基于用户确认的游戏目录重新解析，不再信任安装记录中的绝对路径
- 有效的安装记录目录现在直接表示已安装状态；卸载成功后会删除该目录，不再保留已卸载状态记录
- 安装、卸载和备份模块经过重构，规划、执行、回滚与恢复职责更加清晰

### 破坏性变更

- v0.3.0 使用新的安装记录格式，不支持旧版本安装记录且不会自动迁移；升级前请先使用原版本卸载已安装的 Mod
- 部分应用层与 Assets 访问公共契约已精简或调整，依赖这些 API 的外部集成需要相应更新

---

This release focuses on safer installation and uninstallation, automatic recovery after interrupted operations, and stricter protection for game files and mod layering.

### Added

- The app now checks the latest GitHub release on startup; network and malformed-response failures remain non-fatal
- Install and uninstall operations now record progress in transaction journals, allowing unfinished operations to recover on restart; operations that cannot be recovered safely are quarantined
- Uninstall previews now show later conflicting mods that must be removed first, together with their overlapping assets files
- Install records now store a game-instance fingerprint, install sequence, and file length and SHA-256 integrity data for assets, backups, and payloads

### Fixed

- Mods that modify the same assets file must now be uninstalled in reverse installation order, preventing an older backup from overwriting a later mod
- Uninstall now validates the integrity of current assets, install backups, and payload files before mutation; modified or unreadable files block unsafe replacement or deletion
- If a later assets restore fails during a multi-file uninstall, files already restored are rolled back to their pre-uninstall state
- Install targets and uninstall record paths now reject absolute paths, traversal, and reparse-point escapes to prevent access outside trusted directories
- Assets field validation now preserves actual scalar types such as integers and floating-point values, avoiding incorrect matches or precision loss from string conversion
- Assets read sessions can now be closed safely before writes and reopened when needed, preventing leftover handles from blocking file replacement

### Improved

- Install and uninstall mutations are serialized with a shared lock to prevent concurrent changes to backup records or game files
- Uninstall paths are re-derived from the user-confirmed game directory instead of trusting absolute paths stored in records
- A valid install record directory now directly represents an installed mod; successful uninstall removes the directory instead of retaining an uninstalled status
- Installation, uninstallation, and backup components were reorganized to separate planning, execution, rollback, and recovery more clearly

### Breaking Changes

- v0.3.0 uses a new install record format. Older records are unsupported and are not migrated automatically; uninstall existing mods with the original version before upgrading
- Several application-layer and assets-access public contracts were simplified or changed, so external integrations using these APIs must be updated

## v0.2.0

本次版本重点改进 Mod 安装体验、安装失败回滚能力和终端界面可读性。

### 新增

- Manifest 现在支持 `optional` 附加内容分组，安装时可以选择要应用的可选内容
- 安装结果和安装记录会保存本次实际应用的附加内容，方便后续确认和卸载

### 修复

- 安装过程中如果部分补丁或文件复制失败，程序会回滚已经写入的内容，减少半安装状态
- 覆盖 Assets 文件时会防止符号链接重定向，避免写入到预期目标之外的位置
- 安装写入流程修复了部分 TOCTOU 竞态风险，目标检查和实际写入之间更加安全
- Mod 包解压总大小限制现在会正确覆盖更多安装路径，避免压缩包绕过限制
- 终端页面发生错误时会正确显示错误信息
- Assets 字段树缓存现在限定在查询上下文生命周期内，避免跨查询复用导致结果不准确

### 改进

- 终端界面采用 One Dark 配色，并优化了列表列宽、选中项说明文本和确认提示交互
- 可选内容选择改为多选提示，批量选择更直接
- 列表和摘要渲染改用 Spectre 表格与网格，长内容展示更稳定
- 安装记录读取会先按状态过滤，再反序列化完整记录，大量历史记录场景下更快
- 安装工作流和备份存储结构经过整理，代码路径更清晰，后续维护更容易

---

This release focuses on mod installation usability, safer rollback behavior, and clearer terminal output.

### Added

- Manifests now support `optional` content groups, letting users choose optional content during installation
- Installation results and records now store the optional content that was actually applied, making later verification and uninstall support clearer

### Fixed

- If part of an install fails while applying patches or copying files, the tool now rolls back already-written changes to reduce partial install states
- Assets overwrite paths now defend against symlink redirection, preventing writes outside the intended target
- Install writes now avoid several TOCTOU race risks between target validation and actual writes
- Total mod package extraction limits now apply across more install paths, preventing packages from bypassing the configured cap
- Terminal pages now show errors correctly when a page operation fails
- Assets field tree caching is now scoped to the query context lifetime, avoiding inaccurate results from cross-query reuse

### Improved

- The terminal UI now uses a One Dark color palette and has improved list column widths, selected item descriptions, and confirmation prompts
- Optional content selection now uses a multi-selection prompt for easier batch selection
- List and summary rendering now use Spectre tables and grids for more stable long-content output
- Install record loading filters by status before fully deserializing records, improving performance with many historical records
- Install workflow orchestration and backup storage were consolidated, making the implementation easier to maintain

## v0.1.0

欢迎体验 Unity Assets Patcher 的第一个正式版本！

### 新增

- 终端界面现在支持多语言显示（en-US, zh-Hans）
- 新增模组卸载功能，程序会根据安装时保存的记录恢复被修改的 Assets 文件，并清理安装时复制的文件

### 修复

- 恶意构造的压缩包不再会导致程序长时间卡住或崩溃，现在 `manifest.json` 被限制在 10MB，而 Mod 包解压后的大小占用不会超过 `10GB`
- 本地化文本不再显示占位符键值，菜单、提示和摘要会正确显示当前语言的实际文案
- 读取 Assets 数组元素时兼容性更好，字段读取时会忽略数组大小等元数据节点
- 清理流程失败时也会正确释放已占用的资源，避免文件句柄残留影响后续写入或删除
- 补丁值超出范围时会给出正确提示，尤其是无法写入目标字段的浮点数值
- 不安全的文件路径现在会被正确拦截，避免 Mod 包通过相对路径写入目标目录之外的位置
- 列表结果数量限制现在会正确生效，避免一次性输出过多内容
- VDF 解析器在特殊输入下更加稳定，读取 Steam 库配置时不再依赖易失效的生成式正则
- 终端输出很长时，底部快捷键文字不再错乱
- 从子页面返回后会保留主菜单的选中状态，不再每次都回到默认选项

### 改进

- 安装预览摘要更简洁，补丁修改、备份和文件复制等重点变化更容易看清
- 安装流程和补丁字段查找更快，重复读取 Mod 包和遍历字段树的开销更少
- 终端界面的交互和渲染更加稳定，页面、提示和列表输出的表现更一致

---

Welcome to the first release of Unity Assets Patcher!

### Added

- The terminal interface now supports multiple languages (en-US, zh-Hans)
- Added mod uninstall support. The tool can restore modified Assets files from install records and clean up files copied during installation

### Fixed

- Maliciously crafted packages can no longer make the tool hang for a long time or crash. `manifest.json` is capped at 10MB, and extracted mod package contents are capped at `10GB`
- Localized text no longer shows placeholder keys. Menus, prompts, and summaries now display the actual text for the current language
- Improved compatibility when reading Assets array elements by ignoring metadata nodes such as array size fields
- Cleanup failures now still release acquired resources, preventing leftover file handles from blocking later writes or deletes
- Out-of-range patch values now produce a proper error, especially for floating-point values that cannot be written to the target field
- Unsafe file paths are now blocked correctly, preventing mod packages from writing outside the target directory through relative paths
- List result limits now apply correctly, avoiding overly large output
- The VDF parser is more stable on edge-case input and no longer depends on fragile generated regular expressions when reading Steam library configuration
- Long terminal output no longer corrupts the footer shortcut text
- Returning from a subpage now keeps the main menu selection instead of resetting to the default option

### Improved

- Install preview summaries are easier to scan, with patch changes, backups, and copied files called out more clearly
- Installation and patch field lookup are faster, with less repeated mod package reading and field tree traversal
- Terminal interactions and rendering are more stable, with more consistent behavior across pages, prompts, and lists

## v0.1.0-alpha.2

### 新增

- 工具顶部大标题现在会显示当前版本号，方便确认正在运行的发布版本

### 修复

- JSON 解析非对象数组时会正确报错，不再把无效结构误判为可用配置
- 发布包不再包含 PDB 调试文件，下载体积更小

### 改进

- 发布包使用 NativeAOT 编译，启动更快，也不需要预先安装 .NET 运行时
- 安装流程改为单次文件扫描，减少重复读取带来的等待时间
- 终端界面渲染统一优化，页面刷新和列表展示更加流畅
- 补丁规划、字段查找和 JSON 处理等核心模块经过重构，整体稳定性更好

---

### Added

- The title banner now shows the current version, making it easier to confirm which release is running

### Fixed

- JSON arrays that do not contain objects now fail with the correct error instead of being treated as usable configuration
- Release packages no longer include PDB debug files, reducing download size

### Improved

- Release packages are now built with NativeAOT, making startup faster and removing the need to preinstall the .NET runtime
- Installation now scans files once, reducing wait time from repeated reads
- Terminal rendering has been unified, making page refreshes and list output smoother
- Core modules for patch planning, field lookup, and JSON handling were refactored for better overall stability

## v0.1.0-alpha.1

欢迎体验 Unity Assets Patcher！

这是 Unity Assets Patcher 的首个抢鲜体验版本。如果你在使用过程中遇到问题，或有改进建议，欢迎提交 issue。

注意：当前 release 仅提供 `win-x64` 版本。

---

Welcome to Unity Assets Patcher!

This is the first early access release of Unity Assets Patcher. If you run into issues or have suggestions, open an issue is welcome.

Note: this release is currently available for `win-x64` only.
