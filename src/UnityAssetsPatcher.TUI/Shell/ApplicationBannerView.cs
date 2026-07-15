using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class ApplicationBannerView : View
{
    public ApplicationBannerView(AppInfo appInfo)
    {
        ArgumentNullException.ThrowIfNull(appInfo);

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = 5;

        string version = $" ({appInfo.DisplayVersion})";
        string horizontal = new('─', appInfo.Name.Length + version.Length + 4);
        string emptyLine = $"│{new string(' ', horizontal.Length)}│";

        AddLabel($"╭{horizontal}╮", 0, 0, TerminalGUITheme.Muted);
        AddLabel(emptyLine, 0, 1, TerminalGUITheme.Muted);
        AddLabel("│  ", 0, 2, TerminalGUITheme.Muted);
        AddLabel(appInfo.Name, 3, 2, TerminalGUITheme.Base);
        AddLabel(version, 3 + appInfo.Name.Length, 2, TerminalGUITheme.Muted);
        AddLabel("  │", 3 + appInfo.Name.Length + version.Length, 2, TerminalGUITheme.Muted);
        AddLabel(emptyLine, 0, 3, TerminalGUITheme.Muted);
        AddLabel($"╰{horizontal}╯", 0, 4, TerminalGUITheme.Muted);
    }

    private void AddLabel(string text, int x, int y, Terminal.Gui.Drawing.Scheme scheme)
    {
        var label = new Label { Text = text, X = x, Y = y };
        label.SetScheme(scheme);
        Add(label);
    }
}
