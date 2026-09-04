using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI.Shell;

public sealed class TerminalShellView : Window, ITerminalContentHost
{
    private readonly TerminalPageHeaderView _pageHeader;
    private readonly View _contentHost;
    private readonly Action? _render;
    private View? _content;

    public TerminalShellView(
        string footerText,
        string? warningText = null,
        Action? render = null)
    {
        ArgumentNullException.ThrowIfNull(footerText);

        _render = render;

        Title = AppConfig.Identifier;
        BorderStyle = LineStyle.None;

        SetScheme(TerminalTheme.Base);

        var banner = new ApplicationBannerView();

        _pageHeader = new TerminalPageHeaderView
        {
            Y = Pos.Bottom(banner) + 1
        };

        _contentHost = new View
        {
            X = 0,
            Y = Pos.Bottom(_pageHeader),
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = true
        };

        var footer = new TerminalFooterView(footerText);

        Add(banner, _pageHeader, _contentHost, footer);

        if (warningText is not null)
        {
            Add(new StyledLabel(warningText, TextRole.Preview)
            {
                X = 0,
                Y = Pos.AnchorEnd(2),
                Width = Dim.Fill()
            });
        }
    }

    public void ShowContent(View content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (_content is not null)
        {
            if (_content is TerminalPageView currentPage)
            {
                currentPage.PageHeaderChanged -= OnPageHeaderChanged;
            }

            _contentHost.Remove(_content);

            _content.Dispose();
        }

        _content = content;

        if (content is TerminalPageView page)
        {
            _pageHeader.SetHeader(page.PageTitle, page.PageDescription);
            page.PageHeaderChanged += OnPageHeaderChanged;
        }
        else
        {
            _pageHeader.ClearHeader();
        }

        if (content is ITerminalRenderRequester renderRequester && _render is not null)
        {
            renderRequester.RenderRequested += (_, _) => _render();
        }

        content.X = 0;
        content.Y = 0;
        content.Width = Dim.Fill();
        content.Height = Dim.Fill();
        content.CanFocus = true;

        _contentHost.Add(content);
    }

    private void OnPageHeaderChanged(object? sender, EventArgs eventArgs)
    {
        if (sender is TerminalPageView page && ReferenceEquals(page, _content))
        {
            _pageHeader.SetHeader(page.PageTitle, page.PageDescription);
        }
    }
}
