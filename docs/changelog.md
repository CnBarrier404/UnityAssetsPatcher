# Changelog

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
