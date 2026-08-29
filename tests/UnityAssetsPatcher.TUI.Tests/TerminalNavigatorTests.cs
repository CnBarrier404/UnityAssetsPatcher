using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class TerminalNavigatorTests
{
    [Fact]
    public void TerminalNavigator_WhenChineseCultureIsUsed_ShowsChineseHomePage()
    {
        using TerminalShellView shell = new(
            "Footer");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        TerminalNavigator navigator = CreateNavigator(
            shell,
            new CultureInfo("zh-CN"),
            provider.GetRequiredService<IServiceScopeFactory>());

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList firstChoice = mainMenu.SubViews.OfType<ChoiceItemList>().First();
        StyledLabel heading = Assert.Single(mainMenu.SubViews.OfType<StyledLabel>());

        Assert.Equal("主菜单", heading.Text);
        Assert.Equal("  安装 Mod", firstChoice.Button.Text);
    }

    [Fact]
    public void TerminalNavigator_WhenInstallModIsSelected_ShowsInstallModPage()
    {
        using TerminalShellView shell = new("Footer");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var navigator = new TerminalNavigator(
            shell,
            new CultureInfo("en-US"),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AppRuntimeConfig(),
            null);

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        mainMenu.SubViews.OfType<ChoiceItemList>().First().Button.InvokeCommand(Command.Accept);

        Assert.Single(contentHost.SubViews.OfType<InstallModView>());
    }

    private static TerminalNavigator CreateNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory)
    {
        return new TerminalNavigator(
            shell,
            culture,
            scopeFactory,
            new AppRuntimeConfig(),
            null);
    }
}
