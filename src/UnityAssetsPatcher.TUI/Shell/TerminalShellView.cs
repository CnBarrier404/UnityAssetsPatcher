using Terminal.Gui.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalShellView : Window
{
    private const string TerminalTitle = "UnityAssetsPatcher";

    private readonly IApplication _application;
    private readonly View _contentHost;
    private View? _content;

    public TerminalShellView(IApplication application, AppInfo appInfo, string footerText)
    {
        _application = application;
        Title = TerminalTitle;
        BorderStyle = LineStyle.None;
        SetScheme(TerminalTheme.Base);

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

        if (content is ITerminalRenderRequester renderRequester)
        {
            renderRequester.RenderRequested += (_, _) => _application.LayoutAndDraw();
        }

        content.X = 0;
        content.Y = 0;
        content.Width = Dim.Fill();
        content.Height = Dim.Fill();
        content.CanFocus = true;
        _contentHost.Add(content);
    }
}
