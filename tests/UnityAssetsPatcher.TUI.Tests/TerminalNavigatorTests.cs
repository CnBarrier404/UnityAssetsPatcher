using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Updates;
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
    public void TerminalNavigator_WhenInstallModPageIsShown_RendersBeforeInvokingModFilePicker()
    {
        var events = new List<string>();
        using TerminalShellView shell = new(
            "Footer",
            render: () => events.Add("render"));
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var taskRunner = new TerminalTaskRunner(callback => callback());
        var navigator = new TerminalNavigator(
            shell,
            new CultureInfo("en-US"),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AppRuntimeConfig(),
            null,
            taskRunner,
            () =>
            {
                events.Add("picker");
                return null;
            });

        navigator.ShowMainMenu();
        shell.BeginInit();
        shell.EndInit();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        mainMenu.SubViews.OfType<ChoiceItemList>().First().Button.InvokeCommand(Command.Accept);

        Assert.Equal(["render", "picker"], events);
        Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
    }

    [Fact]
    public void ShowAvailableUpdate_WhenAnotherPageIsVisible_ShowsUpdateAfterReturningToMainMenu()
    {
        using TerminalShellView shell = new(
            "Footer");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        TerminalNavigator navigator = CreateNavigator(
            shell,
            new CultureInfo("en-US"),
            provider.GetRequiredService<IServiceScopeFactory>());

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList firstChoice = mainMenu.SubViews.OfType<ChoiceItemList>().First();

        firstChoice.Button.InvokeCommand(Command.Accept);

        var update = new UpdateInfo(
            "v1.3.0",
            new Uri("https://example.com/releases/v1.3.0"),
            new Uri("https://example.com/download/v1.3.0.exe"),
            new string('0', 64));

        navigator.ShowAvailableUpdate(update);

        Assert.Single(contentHost.SubViews.OfType<InstallModView>());

        navigator.ShowMainMenu();

        MainMenuView updatedMainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        var updateLabels = updatedMainMenu.SubViews
            .SelectMany(view => view.SubViews.Append(view))
            .OfType<StyledLabel>()
            .Where(label => label.Text?.ToString().Contains("v1.3.0", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(2, updateLabels.Length);
        Assert.Contains(updateLabels, label => label.Text?.ToString() == "A new version is available: v1.3.0");
        Assert.Contains(
            updateLabels,
            label => label.Text?.ToString() == "Download: https://example.com/releases/v1.3.0");
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
            null,
            new TerminalTaskRunner(callback => callback()),
            static () => null);
    }
}
