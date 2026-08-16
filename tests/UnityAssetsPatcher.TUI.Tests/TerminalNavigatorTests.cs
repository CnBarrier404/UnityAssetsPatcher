using System.Globalization;
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
    public void TerminalNavigator_WhenMenuItemAndBackAreAccepted_CompletesNavigationLoop()
    {
        using TerminalShellView shell = new(
            new AppInfo("Unity Assets Patcher", "dev"),
            "Footer");
        var navigator = new TerminalNavigator(shell, new CultureInfo("en-US"));

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList[] choices = [.. mainMenu.SubViews.OfType<ChoiceItemList>()];

        Assert.Equal(4, choices.Length);
        Assert.Equal("  Install Mod", choices[0].Button.Text);

        choices[0].Button.InvokeCommand(Command.Accept);

        EmptyPageView emptyPage = Assert.Single(contentHost.SubViews.OfType<EmptyPageView>());
        StyledLabel heading = Assert.Single(emptyPage.SubViews.OfType<StyledLabel>());

        Assert.Equal("Install Mod", heading.Text);

        emptyPage.BackButton.InvokeCommand(Command.Accept);

        Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        Assert.Empty(contentHost.SubViews.OfType<EmptyPageView>());
    }

    [Fact]
    public void TerminalNavigator_WhenChineseCultureIsUsed_ShowsChineseHomePage()
    {
        using TerminalShellView shell = new(
            new AppInfo("Unity Assets Patcher", "dev"),
            "Footer");
        var navigator = new TerminalNavigator(shell, new CultureInfo("zh-CN"));

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList firstChoice = mainMenu.SubViews.OfType<ChoiceItemList>().First();
        StyledLabel heading = Assert.Single(mainMenu.SubViews.OfType<StyledLabel>());

        Assert.Equal("主菜单", heading.Text);
        Assert.Equal("  安装 Mod", firstChoice.Button.Text);
    }

    [Fact]
    public void ShowAvailableUpdate_WhenAnotherPageIsVisible_ShowsUpdateAfterReturningToMainMenu()
    {
        using TerminalShellView shell = new(
            new AppInfo("Unity Assets Patcher", "v1.2.3"),
            "Footer");
        var navigator = new TerminalNavigator(shell, new CultureInfo("en-US"));

        navigator.ShowMainMenu();

        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));
        MainMenuView mainMenu = Assert.Single(contentHost.SubViews.OfType<MainMenuView>());
        ChoiceItemList firstChoice = mainMenu.SubViews.OfType<ChoiceItemList>().First();

        firstChoice.Button.InvokeCommand(Command.Accept);

        var update = new AvailableUpdate(
            "v1.3.0",
            new Uri("https://example.com/releases/v1.3.0"),
            new Uri("https://example.com/download/v1.3.0.exe"),
            new string('0', 64));

        navigator.ShowAvailableUpdate(update);

        EmptyPageView emptyPage = Assert.Single(contentHost.SubViews.OfType<EmptyPageView>());

        emptyPage.BackButton.InvokeCommand(Command.Accept);

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
}
