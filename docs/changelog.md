# Changelog

## v0.1.0-alpha.2

### 新增

- 工具的顶部大标题，现在会显示当前版本号

### 修复

- 修复 JSON 解析非对象数组时的错误判断
- 发布包不再包含 PDB 调试文件，减小包体体积

### 改进

- 使用 NativeAOT 编译，启动速度更快，无需预装 .NET 运行时
- 安装流程改为单次文件扫描，提升安装速度
- 终端界面渲染统一优化，整体更流畅
- 补丁规划、字段查找、JSON 处理等核心模块重构，提升稳定性

### 破坏性改动

- 字段路径查找 API 迁移至导航器
- 对象值提取 API 迁移至 `JsonUtils`

---

## v0.1.0-alpha.1

欢迎体验 Unity Assets Patcher！

这是 Unity Assets Patcher 的首个抢鲜体验版本。如果你在使用过程中遇到问题，或有改进建议，欢迎提交 issue。

注意：当前 release 仅提供 `win-x64` 版本。

---

Welcome to Unity Assets Patcher!

This is the first early access release of Unity Assets Patcher. If you run into issues or have suggestions, open an issue is welcome.

Note: this release is currently available for `win-x64` only.
