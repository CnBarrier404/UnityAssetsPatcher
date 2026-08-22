using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.RepositoryManagement;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Uninstallation;
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
    public void TerminalNavigator_WhenInstallModPageIsShown_RendersBeforeInvokingModFilePicker()
    {
        var events = new List<string>();
        using TerminalShellView shell = new(
            new AppInfo("Unity Assets Patcher", "dev"),
            "Footer",
            render: () => events.Add("render"));
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var taskRunner = new TerminalTaskRunner(callback => callback());
        var navigator = new TerminalNavigator(
            shell,
            new CultureInfo("en-US"),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TerminalSettings(),
            null,
            taskRunner,
            static () => { },
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
            new AppInfo("Unity Assets Patcher", "v1.2.3"),
            "Footer");
        var navigator = new TerminalNavigator(shell, new CultureInfo("en-US"));

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

    [Fact]
    public void Start_WhenRepositoryFormatIsUnsupported_RequiresConfirmationBeforeShowingMainMenu()
    {
        using TerminalShellView shell = new(
            new AppInfo("Unity Assets Patcher", "dev"),
            "Footer");
        var clearHandler = new ClearRepositoryHandler();
        var services = new ServiceCollection();
        services.AddScoped<IRepository>(_ => new UnsupportedRepository());
        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        services.AddScoped<
            IRequestHandler<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>>(_ =>
            clearHandler);
        using ServiceProvider provider = services.BuildServiceProvider();
        var taskRunner = new TerminalTaskRunner(callback => callback());
        var navigator = new TerminalNavigator(
            shell,
            new CultureInfo("en-US"),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TerminalSettings(),
            null,
            taskRunner,
            static () => { },
            static () => null);

        navigator.Start();

        Assert.True(SpinWait.SpinUntil(
            () => FindContent<UnsupportedRepositoryView>(shell) is not null && !taskRunner.IsRunning,
            TimeSpan.FromSeconds(5)));
        var unsupported = FindContent<UnsupportedRepositoryView>(shell)!;
        ChoiceItem clearChoice = unsupported.SubViews.OfType<ChoiceItem>().First();

        clearChoice.Button.InvokeCommand(Command.Accept);

        var confirmation =
            Assert.IsType<ClearUnsupportedRepositoryConfirmationView>(CurrentContent(shell));
        ConfirmationBar actions = Assert.Single(confirmation.SubViews.OfType<ConfirmationBar>());
        Assert.Equal(0, clearHandler.CallCount);

        actions.ConfirmButton.InvokeCommand(Command.Accept);

        Assert.True(SpinWait.SpinUntil(
            () => FindContent<MainMenuView>(shell) is not null && !taskRunner.IsRunning,
            TimeSpan.FromSeconds(5)));
        Assert.Equal(1, clearHandler.CallCount);
    }

    private static T? FindContent<T>(TerminalShellView shell) where T : View
    {
        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));

        return contentHost.SubViews.OfType<T>().SingleOrDefault();
    }

    private static View CurrentContent(TerminalShellView shell)
    {
        View contentHost = Assert.Single(shell.SubViews, view => view.GetType() == typeof(View));

        return Assert.Single(contentHost.SubViews);
    }

    private sealed class UnsupportedRepository : IRepository
    {
        public void Initialize()
        {
            throw new UnsupportedRepositoryFormatException(1, 2);
        }

        public IReadOnlyList<InstallRecordSummary> ListInstalledMods()
        {
            throw new NotSupportedException();
        }

        public Task<RepositoryInstallResult> InstallModAsync(
            InstallModPlan plan,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UninstallModResult> UninstallModAsync(
            UninstallPlan plan,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ClearRepositoryHandler :
        IRequestHandler<ClearUnsupportedRepositoryRequest, OperationResult<RepositoryClearResult>>
    {
        public int CallCount { get; private set; }

        public Task<OperationResult<RepositoryClearResult>> HandleAsync(
            ClearUnsupportedRepositoryRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var result = new RepositoryClearResult(1, 2);

            return Task.FromResult<OperationResult<RepositoryClearResult>>(
                new OperationSucceeded<RepositoryClearResult>(result));
        }
    }
}
