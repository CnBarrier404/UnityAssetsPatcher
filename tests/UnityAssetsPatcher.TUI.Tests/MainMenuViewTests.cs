using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Pages.MainMenu;
using UnityAssetsPatcher.TUI.Shell;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class MainMenuViewTests
{
    [Fact]
    public async Task MainMenuView_WhenInitialized_FocusesFirstChoice()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        await using var mainMenuLogic = new MainMenuLogic(scopeFactory);
        using TerminalShellView shell = new("Footer");
        var routeTable = new TerminalRouteTable(
            scopeFactory,
            new AppRuntimeConfig(),
            mainMenuLogic);
        var navigator = new TerminalNavigator(
            shell,
            routeTable.Create(new CultureInfo("en-US")));

        navigator.Navigate(TerminalRoute.MainMenu);

        MainMenuView menu = Assert.Single(
            Assert.Single(shell.SubViews, view => view.GetType() == typeof(View))
                .SubViews
                .OfType<MainMenuView>());
        menu.CanFocus = true;

        menu.BeginInit();

        menu.EndInit();

        ChoiceItemList firstChoice = menu.SubViews.OfType<ChoiceItemList>().First();

        Assert.True(firstChoice.Button.HasFocus);
        Assert.Equal("› Install Mod", firstChoice.Button.Text.ToString());
        Assert.Same(TerminalTheme.Selected, firstChoice.Description.GetScheme());
    }
}
