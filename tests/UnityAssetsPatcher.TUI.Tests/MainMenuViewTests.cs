using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Pages;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class MainMenuViewTests
{
    [Fact]
    public void MainMenuView_WhenInitialized_FocusesFirstChoice()
    {
        TerminalMenuItem[] items =
        [
            new("First", "First description", _ => new View()),
            new("Second", "Second description", _ => new View())
        ];

        using MainMenuView menu = new("Main menu", items);
        menu.CanFocus = true;

        menu.BeginInit();

        menu.EndInit();

        ChoiceItemList firstChoice = menu.SubViews.OfType<ChoiceItemList>().First();

        Assert.True(firstChoice.Button.HasFocus);
        Assert.Equal("› First", firstChoice.Button.Text.ToString());
        Assert.Same(TerminalTheme.Selected, firstChoice.Description.GetScheme());
    }

    [Fact]
    public void ShowAvailableUpdate_WhenCalledTwice_InsertsUpdateWithoutChangingFocus()
    {
        TerminalMenuItem[] items =
        [
            new("First", "First description", _ => new View()),
            new("Second", "Second description", _ => new View())
        ];
        var notice = new TerminalUpdateNotice(
            "A new version is available: v1.3.0",
            "Download: https://example.com/releases/v1.3.0");
        using MainMenuView menu = new("Main menu", items);
        menu.CanFocus = true;

        menu.BeginInit();

        menu.EndInit();

        ChoiceItemList firstChoice = menu.SubViews.OfType<ChoiceItemList>().First();

        menu.ShowAvailableUpdate(notice);

        menu.ShowAvailableUpdate(notice);

        var updateLabels = menu.SubViews
            .SelectMany(view => view.SubViews.Append(view))
            .OfType<StyledLabel>()
            .Where(label => label.Text?.ToString().Contains("v1.3.0", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.True(firstChoice.Button.HasFocus);
        Assert.Equal(2, updateLabels.Length);
    }
}
