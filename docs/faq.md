# 常见问题 - FAQ

## Q1: 为什么提示“你正在使用旧版 Windows 控制台”？

Unity Assets Patcher 的交互式界面需要现代终端提供的字符显示和布局能力。如果程序在旧版 Windows 控制台主机（Console Host）中运行，部分边框、文字或界面布局可能无法正确显示，因此会出现此警告。

建议安装并使用 **Windows Terminal**：

1. 打开 Microsoft Store，搜索并安装 [Windows Terminal](https://aka.ms/terminal)。也可以在 PowerShell 中运行：

   ```powershell
   winget install --id Microsoft.WindowsTerminal -e
   ```

2. 打开 Windows Terminal，进入“设置” > “启动”，将“默认终端应用程序”切换为“Windows Terminal”，然后保存设置。
3. 关闭当前的旧版控制台窗口，再重新运行 Unity Assets Patcher。已经打开的窗口不会在切换设置后自动迁移到 Windows Terminal。

如果设置中没有“默认终端应用程序”选项，请先更新 Windows 和 Windows Terminal。你也可以直接打开 Windows Terminal，在其中进入程序所在目录并运行：

```powershell
.\UnityAssetsPatcher.exe
```

当程序在 Windows Terminal 的标签页中运行时，该警告应不再出现。
