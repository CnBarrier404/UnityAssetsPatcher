using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalShellView : Window
{
    private readonly View _contentHost;
    private View? _content;

    public TerminalShellView(AppInfo appInfo, string footerText)
    {
        BorderStyle = LineStyle.None;
        SetScheme(TerminalGUITheme.Base);

        var banner = new ApplicationBannerView(appInfo);
        _contentHost = new View
        {
            X = 0,
            Y = 6,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = true,
        };
        var footer = new TerminalFooterView(footerText);
        Add(banner, _contentHost, footer);
    }

    public void ShowContent(View content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (_content is not null)
        {
            _contentHost.Remove(_content);
            _content.Dispose();
        }

        _content = content;
        content.X = 0;
        content.Y = 0;
        content.Width = Dim.Fill();
        content.Height = Dim.Fill();
        content.CanFocus = true;
        _contentHost.Add(content);
    }
}
