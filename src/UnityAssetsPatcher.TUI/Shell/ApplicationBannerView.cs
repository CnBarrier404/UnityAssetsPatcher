using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class ApplicationBannerView : View
{
    public ApplicationBannerView()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = 5;

        string version = $" ({AppConfig.DisplayVersion})";
        string horizontal = new('─', AppConfig.Name.Length + version.Length + 4);
        string emptyLine = $"│{new string(' ', horizontal.Length)}│";

        AddLabel($"╭{horizontal}╮", 0, 0, TerminalTheme.Muted);
        AddLabel(emptyLine, 0, 1, TerminalTheme.Muted);
        AddLabel("│  ", 0, 2, TerminalTheme.Muted);
        AddLabel(AppConfig.Name, 3, 2, TerminalTheme.Base);
        AddLabel(version, 3 + AppConfig.Name.Length, 2, TerminalTheme.Muted);
        AddLabel("  │", 3 + AppConfig.Name.Length + version.Length, 2, TerminalTheme.Muted);
        AddLabel(emptyLine, 0, 3, TerminalTheme.Muted);
        AddLabel($"╰{horizontal}╯", 0, 4, TerminalTheme.Muted);
    }

    private void AddLabel(string text, int x, int y, Scheme scheme)
    {
        var label = new StyledLabel(text)
        {
            X = x,
            Y = y
        };

        label.SetScheme(scheme);

        Add(label);
    }
}
