using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Pages.MainMenu;
using UnityAssetsPatcher.TUI.Pages.Settings;
using UnityAssetsPatcher.TUI.Shell;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class TerminalNavigatorTests
{
    [Fact]
    public async Task TerminalNavigator_WhenChineseCultureIsUsed_ShowsChineseHomePage()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        await using var mainMenuLogic = new MainMenuLogic(scopeFactory);
        using TerminalShellView shell = new("Footer");
        TerminalNavigator navigator = CreateNavigator(
            shell,
            new CultureInfo("zh-CN"),
            scopeFactory,
            mainMenuLogic);

        navigator.Navigate(TerminalRoute.MainMenu);

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList firstChoice = mainMenu.SubViews.OfType<ChoiceItemList>().First();
        TerminalPageHeaderView header = Assert.Single(shell.SubViews.OfType<TerminalPageHeaderView>());
        StyledLabel heading = header.SubViews.OfType<StyledLabel>().First();

        Assert.Equal("主菜单", heading.Text);
        Assert.Equal("  安装 Mod", firstChoice.Button.Text);
    }

    [Fact]
    public async Task TerminalNavigator_WhenInstallModIsSelected_ShowsInstallModPage()
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

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        mainMenu.SubViews.OfType<ChoiceItemList>().First().Button.InvokeCommand(Command.Accept);

        Assert.Single(contentHost.SubViews.OfType<InstallModView>());
    }

    [Fact]
    public async Task TerminalNavigator_WhenVerboseLoggingIsToggled_UpdatesRuntimeSettings()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        await using var mainMenuLogic = new MainMenuLogic(scopeFactory);
        using TerminalShellView shell = new("Footer");
        var runtimeConfig = new AppRuntimeConfig();
        var loggingLevelSwitch = new StubLoggingLevelSwitch();
        var routeTable = new TerminalRouteTable(
            scopeFactory,
            runtimeConfig,
            mainMenuLogic,
            loggingLevelSwitch);
        var navigator = new TerminalNavigator(
            shell,
            routeTable.Create(new CultureInfo("en-US")));

        navigator.Navigate(TerminalRoute.MainMenu);

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        mainMenu.SubViews
            .OfType<ChoiceItemList>()
            .ElementAt(3)
            .Button
            .InvokeCommand(Command.Accept);

        SettingsView settings = Assert.Single(contentHost.SubViews.OfType<SettingsView>());
        ToggleItem verboseOutput = Assert.Single(settings.SubViews.OfType<ToggleItem>());
        verboseOutput.Button.InvokeCommand(Command.Accept);

        Assert.True(runtimeConfig.VerboseLogging);
        Assert.Equal(LoggingLevel.Debug, loggingLevelSwitch.MinimumLevel);
    }

    private static TerminalNavigator CreateNavigator(
        TerminalShellView shell,
        CultureInfo culture,
        IServiceScopeFactory scopeFactory,
        MainMenuLogic mainMenuLogic)
    {
        var routeTable = new TerminalRouteTable(
            scopeFactory,
            new AppRuntimeConfig(),
            mainMenuLogic);

        return new TerminalNavigator(
            shell,
            routeTable.Create(culture));
    }

    private sealed class StubLoggingLevelSwitch : ILoggingLevelSwitch
    {
        public LoggingLevel MinimumLevel { get; set; }
    }
}
