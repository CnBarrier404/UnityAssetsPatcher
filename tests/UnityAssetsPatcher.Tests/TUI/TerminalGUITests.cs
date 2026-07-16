using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Pages;
using UnityAssetsPatcher.TUI.Shell;
using Xunit;

namespace UnityAssetsPatcher.Tests.TUI;

public sealed class TerminalGUITests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public TerminalGUITests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [Fact]
    public void ViewConstructors_DoNotUseServiceProviderOrContextObjects()
    {
        Type[] viewTypes = typeof(TerminalApp).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, Namespace: "UnityAssetsPatcher.TUI.Pages" } &&
                           typeof(View).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(viewTypes);

        foreach (Type viewType in viewTypes)
        {
            foreach (ParameterInfo parameter in viewType
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .SelectMany(constructor => constructor.GetParameters()))
            {
                Assert.NotEqual(typeof(IServiceProvider), parameter.ParameterType);
                Assert.False(
                    parameter.ParameterType.Name.EndsWith("Context", StringComparison.Ordinal),
                    $"{viewType.Name} constructor depends on context type {parameter.ParameterType.Name}.");
            }
        }
    }

    [Fact]
    public void MainMenuView_FocusesFirstChoice()
    {
        TerminalMenuItem[] items =
        [
            new("First", "First description", _ => new View()),
            new("Second", "Second description", _ => new View()),
        ];

        using var menu = new MainMenuView(items, null);
        menu.CanFocus = true;
        menu.BeginInit();
        menu.EndInit();
        ChoiceItem firstChoice = menu.SubViews.OfType<ChoiceItem>().First();

        Assert.True(firstChoice.Button.HasFocus);
        Assert.Equal("> First", firstChoice.Button.Text.ToString());
        Assert.Same(TerminalTheme.Selected, firstChoice.Description.GetScheme());
    }

    [Fact]
    public void AddUnityAssetsPatcherTui_RegistersTerminalAppForOfficialServiceProvider()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAssetsAccessScopeFactory>(new ThrowingAssetsAccessScopeFactory())
            .AddUnityAssetsPatcherApplication("backup")
            .AddUnityAssetsPatcherTUI(new AppInfo("Unity Assets Patcher", "dev"))
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.NotNull(provider.GetRequiredService<TerminalApp>());
    }

    [Fact]
    public void TerminalPathNormalizer_Normalize_RemovesQuotesAddedByTerminalDragAndDrop()
    {
        Assert.Equal(
            @"D:\Mods\Example Mod.zip",
            TerminalPathNormalizer.Normalize("  \"D:\\Mods\\Example Mod.zip\"  "));
        Assert.Equal(
            @"D:\Games\Example Game",
            TerminalPathNormalizer.Normalize("'D:\\Games\\Example Game'"));
    }

    [Fact]
    public void FrameworkControls_ApplyProjectDefaults()
    {
        using var action = new ActionButton("Save");
        using var input = new InputField();
        using var output = new TextViewer("result");
        using var label = new StyledLabel("Title", TextRole.Title);

        Assert.Equal("  Save", action.Text.ToString());
        Assert.True(action.NoDecorations);
        Assert.True(action.NoPadding);
        Assert.Same(TerminalTheme.Interactive, action.GetScheme());
        Assert.Same(TerminalTheme.Interactive, input.GetScheme());
        Assert.True(output.ReadOnly);
        Assert.Equal("result", output.Text.ToString());
        Assert.Same(TerminalTheme.Title, label.GetScheme());
    }

    [Fact]
    public void ToggleItem_ReflectsSelectionAndRaisesChangeEvent()
    {
        using var choice = new ToggleItem("Verbose", "Show details");
        int changes = 0;
        choice.IsSelectedChanged += (_, _) => changes++;

        choice.IsSelected = true;

        Assert.True(choice.IsSelected);
        Assert.Equal(1, changes);
        Assert.Equal("  [*] Verbose", choice.Button.Text.ToString());
        Assert.Equal("Show details", choice.Description.Text.ToString());
        Assert.Same(TerminalTheme.Muted, choice.Description.GetScheme());
    }

    [Fact]
    public void ConfirmationBar_InvokesConfiguredActions()
    {
        int confirms = 0;
        int cancels = 0;
        using var actions = new ConfirmationBar(
            "Install",
            () => confirms++,
            "Back",
            () => cancels++);

        actions.ConfirmButton.InvokeCommand(Command.Accept);
        actions.CancelButton.InvokeCommand(Command.Accept);

        Assert.Equal(1, confirms);
        Assert.Equal(1, cancels);
        Assert.Equal("  Install", actions.ConfirmButton.Text.ToString());
        Assert.Equal("  Back", actions.CancelButton.Text.ToString());
        Assert.Equal(1, actions.Height);
        Assert.Same(TerminalTheme.PrimaryAction, actions.ConfirmButton.GetScheme());
        Assert.Same(TerminalTheme.SecondaryAction, actions.CancelButton.GetScheme());
    }

    [Fact]
    public void SummaryTableView_UsesDisplayWidthForLabelColumn()
    {
        using var table = new SummaryTableView([("版本", "1.0"), ("Author", "Test")]);
        ITableSource source = Assert.IsAssignableFrom<ITableSource>(table.Table);

        Assert.False(table.CanFocus);
        Assert.Equal(2, source.Rows);
        Assert.Equal(2, source.Columns);
        Assert.Equal(9, table.Style.ColumnStyles[0].MinWidth);
        Assert.Equal(9, table.Style.ColumnStyles[0].MaxWidth);
        Assert.False(table.Style.ShowHeaders);
    }

    [Fact]
    public void ScrollableContentView_WhenFocusedControlIsBelowViewport_ScrollsItIntoView()
    {
        using var content = new ScrollableContentView
        {
            Width = 40,
            Height = 5,
        };
        using var actions = new ConfirmationBar("Continue", () => { }, "Back", () => { })
            { X = 0, Y = 15 };
        content.Add(actions);
        content.SetContentHeightForRows(17);
        content.BeginInit();
        content.EndInit();
        content.Layout(new Size(40, 5));

        actions.ConfirmButton.SetFocus();

        Assert.True(content.Viewport.Y > 0);
        Assert.True(actions.ConfirmButton.FrameToScreen().Bottom <= content.ViewportToScreen().Bottom);
        Assert.True(content.VerticalScrollBar.Visible);
    }

    [Fact]
    public void ScrollableContentView_WhenContentFitsViewport_DoesNotShowScrollBar()
    {
        using var content = new ScrollableContentView
        {
            Width = 40,
            Height = 5,
        };
        content.SetContentHeightForRows(3);
        content.BeginInit();
        content.EndInit();
        content.Layout(new Size(40, 5));

        Assert.Equal(0, content.Viewport.Y);
        Assert.False(content.VerticalScrollBar.Visible);
    }

    [Fact]
    public void ScrollableContentView_WhenMouseWheelIsUsedOverChildControl_ScrollsContent()
    {
        using var content = new ScrollableContentView
        {
            Width = 40,
            Height = 5,
        };
        using var label = new StyledLabel("Content") { X = 0, Y = 0 };
        content.Add(label);
        content.SetContentHeightForRows(20);
        content.BeginInit();
        content.EndInit();
        content.Layout(new Size(40, 5));
        var mouse = new Mouse { Flags = MouseFlags.WheeledDown };

        label.RaiseMouseEvent(mouse);

        Assert.Equal(3, content.Viewport.Y);
        Assert.True(mouse.Handled);
    }

    [Fact]
    public void TerminalTaskRunner_RunsWorkInBackgroundAndDispatchesSuccess()
    {
        var dispatched = new ConcurrentQueue<Action>();
        using var operationStarted = new ManualResetEventSlim();
        using var releaseOperation = new ManualResetEventSlim();
        var runner = new TerminalTaskRunner(dispatched.Enqueue);
        int callingThread = Environment.CurrentManagedThreadId;
        int operationThread = 0;
        int callbackThread = 0;
        int result = 0;

        Assert.True(runner.TryRun(
            () =>
            {
                operationThread = Environment.CurrentManagedThreadId;
                operationStarted.Set();
                releaseOperation.Wait();
                return 42;
            },
            value =>
            {
                callbackThread = Environment.CurrentManagedThreadId;
                result = value;
            },
            _ => throw new Xunit.Sdk.XunitException("The operation should not fail.")));

        Assert.True(operationStarted.Wait(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
        Assert.True(runner.IsRunning);
        Assert.False(runner.TryRun(() => 0, _ => { }, _ => { }));

        releaseOperation.Set();
        Assert.True(SpinWait.SpinUntil(() => !dispatched.IsEmpty, TimeSpan.FromSeconds(5)));
        Assert.True(dispatched.TryDequeue(out Action? callback));
        callback();

        Assert.NotEqual(callingThread, operationThread);
        Assert.Equal(callingThread, callbackThread);
        Assert.Equal(42, result);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public void TerminalTaskRunner_DispatchesFailureAndReleasesRunner()
    {
        var dispatched = new ConcurrentQueue<Action>();
        var runner = new TerminalTaskRunner(dispatched.Enqueue);
        Exception? actualException = null;
        var expectedException = new InvalidOperationException("Failure");

        Assert.True(runner.TryRun<int>(
            () => throw expectedException,
            _ => throw new Xunit.Sdk.XunitException("The operation should not succeed."),
            exception => actualException = exception));

        Assert.True(SpinWait.SpinUntil(() => !dispatched.IsEmpty, TimeSpan.FromSeconds(5)));
        Assert.True(dispatched.TryDequeue(out Action? callback));
        callback();

        Assert.Same(expectedException, actualException);
        Assert.False(runner.IsRunning);
    }

    private sealed class ThrowingAssetsAccessScopeFactory : IAssetsAccessScopeFactory
    {
        public IAssetsAccessScope CreateScope()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingWorkflowService : IWorkflowService
    {
        public void RecoverPendingTransactions() => throw new NotSupportedException();

        public ModManifest CheckManifest(string path) => throw new NotSupportedException();

        public InspectListResult InspectList(InspectListRequest request) => throw new NotSupportedException();

        public AssetsFieldInfo InspectFields(InspectFieldsRequest request) => throw new NotSupportedException();

        public InstallPreviewResult PreviewInstall(InstallRequest request) => throw new NotSupportedException();

        public InstallModResult Install(InstallRequest request) => throw new NotSupportedException();

        public IReadOnlyList<InstallRecordSummary> ListInstalledMods() => [];

        public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request) =>
            throw new NotSupportedException();

        public UninstallModResult Uninstall(UninstallModRequest request) => throw new NotSupportedException();
    }
}
