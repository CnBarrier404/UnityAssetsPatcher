using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Pages.Settings;
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

        navigator.Navigate(TerminalRoute.MainMenu);

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
            TerminalRouteTable.Create(
                new CultureInfo("en-US"),
                provider.GetRequiredService<IServiceScopeFactory>(),
                new AppRuntimeConfig(),
                null));

        navigator.Navigate(TerminalRoute.MainMenu);

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        mainMenu.SubViews.OfType<ChoiceItemList>().First().Button.InvokeCommand(Command.Accept);

        Assert.Single(contentHost.SubViews.OfType<InstallModView>());
    }

    [Fact]
    public void TerminalNavigator_WhenVerboseLoggingIsToggled_UpdatesRuntimeSettings()
    {
        using TerminalShellView shell = new("Footer");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var runtimeConfig = new AppRuntimeConfig();
        var loggingLevelSwitch = new StubLoggingLevelSwitch();
        var navigator = new TerminalNavigator(
            shell,
            TerminalRouteTable.Create(
                new CultureInfo("en-US"),
                provider.GetRequiredService<IServiceScopeFactory>(),
                runtimeConfig,
                loggingLevelSwitch));

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
        IServiceScopeFactory scopeFactory)
    {
        return new TerminalNavigator(
            shell,
            TerminalRouteTable.Create(
                culture,
                scopeFactory,
                new AppRuntimeConfig(),
                null));
    }

    private sealed class StubLoggingLevelSwitch : ILoggingLevelSwitch
    {
        public LoggingLevel MinimumLevel { get; set; }
    }
}
