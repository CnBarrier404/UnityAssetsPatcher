# Changelog

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
