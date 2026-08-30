using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Shell;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class TerminalShellViewTests
{
    [Fact]
    public void TerminalShellView_WhenWarningIsProvided_PreservesShellLayout()
    {
        using TerminalShellView shell = new(
            "Footer",
            "Legacy console warning");

        StyledLabel warning = Assert.Single(shell.SubViews.OfType<StyledLabel>());
        TerminalFooterView footer = Assert.Single(shell.SubViews.OfType<TerminalFooterView>());
        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));

        Assert.Equal(AppConfig.Identifier, shell.Title);
        Assert.Equal("Legacy console warning", warning.Text.ToString());
        Assert.Same(TerminalTheme.Preview, warning.GetScheme());
        Assert.Equal(Pos.AnchorEnd(2), warning.Y);
        Assert.Equal(Pos.AnchorEnd(1), footer.Y);
        Assert.Equal(Dim.Fill(2), contentHost.Height);
    }
}
