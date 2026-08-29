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
}
