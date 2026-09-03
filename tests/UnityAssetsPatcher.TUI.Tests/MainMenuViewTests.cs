using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class MainMenuViewTests
{
    [Fact]
    public void MainMenuView_WhenInitialized_FocusesFirstChoice()
    {
        MainMenuItem[] items =
        [
            new("First", "First description", TerminalRoute.InstallMod),
            new("Second", "Second description", TerminalRoute.UninstallMod)
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
