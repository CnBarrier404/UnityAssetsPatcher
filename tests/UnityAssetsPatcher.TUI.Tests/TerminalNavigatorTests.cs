using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Lifecycle;
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
            new ImmediateUIDispatcher(),
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
            new ImmediateUIDispatcher(),
            new TerminalTaskRunner(callback => callback()),
            static () => null);
    }

    private sealed class ImmediateUIDispatcher : ITerminalUIDispatcher
    {
        public bool TryInvoke(Action callback, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            callback();

            return true;
        }
    }
}
