using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalShellView : Window
{
    private readonly string _defaultFooterText;
    private readonly View _contentHost;
    private readonly TerminalFooterView _footer;
    private View? _content;

    public TerminalShellView(AppInfo appInfo, string footerText)
    {
        _defaultFooterText = footerText;
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
        _footer = new TerminalFooterView(footerText);
        Add(banner, _contentHost, _footer);
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
        _footer.SetText(content is ITerminalContentView terminalContent
            ? terminalContent.ShortcutHint
            : _defaultFooterText);
        content.X = 0;
        content.Y = 0;
        content.Width = Dim.Fill();
        content.Height = Dim.Fill();
        content.CanFocus = true;
        _contentHost.Add(content);
    }

}
