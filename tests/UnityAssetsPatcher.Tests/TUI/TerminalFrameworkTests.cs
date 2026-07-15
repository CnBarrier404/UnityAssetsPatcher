using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
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

public sealed class TerminalFrameworkTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public TerminalFrameworkTests()
    {
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [Fact]
    public void PageConstructors_DoNotUseServiceProviderOrContextObjects()
    {
        var tuiAssembly = typeof(TerminalApp).Assembly;

        Type[] pageTypes = tuiAssembly
            .GetTypes()
            .Where(type => type is { IsClass: true, Namespace: "UnityAssetsPatcher.TUI.Pages" })
            .ToArray();

        Assert.NotEmpty(pageTypes);

        foreach (Type pageType in pageTypes)
        {
            foreach (ParameterInfo parameter in pageType
                         .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .SelectMany(constructor => constructor.GetParameters()))
            {
                Assert.NotEqual(typeof(IServiceProvider), parameter.ParameterType);
                Assert.False(
                    parameter.ParameterType.Name.EndsWith("Context", StringComparison.Ordinal),
                    $"{pageType.Name} constructor depends on context type {parameter.ParameterType.Name}.");
            }
        }
    }

    [Fact]
    public void PageConstructors_DoNotDependOnLowLevelTerminalServices()
    {
        var forbiddenTypes = new HashSet<Type>
        {
            typeof(Spectre.Console.IAnsiConsole),
            typeof(TerminalUI),
            typeof(TerminalPrompts),
        };
        Type[] pageTypes = typeof(TerminalApp).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, Namespace: "UnityAssetsPatcher.TUI.Pages" } &&
                           typeof(ITerminalPage).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(pageTypes);

        foreach (Type pageType in pageTypes)
        {
            ParameterInfo[] parameters = pageType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .ToArray();

            Assert.DoesNotContain(parameters, parameter => forbiddenTypes.Contains(parameter.ParameterType));
        }
    }

    [Fact]
    public void ViewConstructors_DoNotDependOnAnsiConsoleDirectly()
    {
        Type[] viewTypes = typeof(TerminalApp).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, Namespace: "UnityAssetsPatcher.TUI.Pages" } &&
                           type.Name.EndsWith("TerminalView", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(viewTypes);

        foreach (Type viewType in viewTypes)
        {
            ParameterInfo[] parameters = viewType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .ToArray();

            Assert.DoesNotContain(parameters,
                parameter => parameter.ParameterType == typeof(Spectre.Console.IAnsiConsole));
        }
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
    public void InstallPage_CreateView_UsesTerminalGuiContentPage()
    {
        var page = new InstallTerminalPage(new ThrowingWorkflowService(), new TerminalSettings());

        using Terminal.Gui.ViewBase.View view = page.CreateView(() => { });

        Assert.IsAssignableFrom<ITerminalGUIPage>(page);
        Assert.IsType<InstallModView>(view);
    }

    [Fact]
    public void SettingsPage_CreateView_UsesTerminalGuiContentPage()
    {
        var page = new SettingsTerminalPage(new TerminalSettings());

        using Terminal.Gui.ViewBase.View view = page.CreateView(() => { });

        Assert.IsAssignableFrom<ITerminalGUIPage>(page);
        Assert.IsType<SettingsView>(view);
        var content = Assert.IsAssignableFrom<ITerminalContentView>(view);
        Assert.Equal("Shortcuts: ↑/↓ to choose | Space to toggle | Esc to cancel | Ctrl + C to exit",
            content.ShortcutHint);
    }

    [Fact]
    public void UninstallPage_CreateView_UsesTerminalGuiContentPage()
    {
        var page = new UninstallTerminalPage(new ThrowingWorkflowService());

        using Terminal.Gui.ViewBase.View view = page.CreateView(() => { });

        Assert.IsAssignableFrom<ITerminalGUIPage>(page);
        Assert.IsType<UninstallModView>(view);
    }

    [Fact]
    public void AddUnityAssetsPatcherTui_RegistersTerminalAppForOfficialServiceProvider()
    {
        TestConsole console = CreateConsole();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IAssetsAccessScopeFactory>(new ThrowingAssetsAccessScopeFactory())
            .AddUnityAssetsPatcherApplication("backup")
            .AddUnityAssetsPatcherTUI(
                new AppInfo("Unity Assets Patcher", "dev"),
                console)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        TerminalApp app = provider.GetRequiredService<TerminalApp>();

        Assert.NotNull(app);
    }

    [Fact]
    public void Layout_ShowPageAndPrepareOutputArea_DelegatesLayoutRendering()
    {
        TestConsole console = CreateConsole().Height(10);
        var ui = new TerminalUI(console, new AppInfo("Example Tool", "v1.2.3"));

        ui.Layout.ShowPage("Task Runner", "Choose an action first.");
        ui.Layout.PrepareOutputArea();

        string output = console.Output;
        Assert.Contains("Example Tool (v1.2.3)", output);
        Assert.Contains("Task Runner", output);
        Assert.Contains("Choose an action first.", output);
        Assert.Contains("\e[s", output);
        Assert.Contains("\e[u", output);
    }

    [Fact]
    public void Lists_WriteDescribedList_RendersLabelsDescriptionsAndSelection()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.List.WriteDescribedList(
            [
                new TerminalChoiceDisplay("Primary action", "Run the primary task."),
                new TerminalChoiceDisplay("Preferences", "Adjust output detail."),
            ],
            selectedIndex: 0);

        string output = console.Output;
        Assert.Contains("> Primary action", output);
        Assert.Contains("Run the primary task.", output);
        Assert.Contains("  Preferences", output);
        Assert.Contains("Adjust output detail.", output);
    }

    [Fact]
    public void Lists_WriteToggleList_RendersRowsWithIndicatorAndCheckbox()
    {
        TestConsole console = CreateConsole().SupportsAnsi(true);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.List.WriteToggleList(
            [
                new TerminalToggleDisplay("详细日志", "显示详细安装预览日志。", false),
                new TerminalToggleDisplay("安装耗时详情", "显示各阶段耗时。", true),
            ],
            selectedIndex: 0);

        string output = console.Output;
        Assert.Contains("详细日志", output);
        Assert.Contains("安装耗时详情", output);
        Assert.Contains("显示详细安装预览日志。", output);
        Assert.Contains("显示各阶段耗时。", output);
        Assert.Contains("[X]", output);
        Assert.Contains("[ ]", output);
    }

    [Fact]
    public void Lists_WriteToggleList_WrapsLongLabelsAndDescriptionsToConsoleWidth()
    {
        TestConsole console = CreateConsole().Width(42);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.List.WriteToggleList(
            [
                new TerminalToggleDisplay(
                    "非常非常长的设置标题",
                    "这是一段很长的设置说明文本，需要在标准终端宽度内自动换行。",
                    false),
            ],
            selectedIndex: 0);

        string[] lines = console.Output
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length > 1, "Expected wrapping to produce multiple lines.");
        Assert.Contains("非常非常长的设置", console.Output);
        Assert.Contains("这是一段很长的设置", console.Output);
    }

    [Fact]
    public void Summary_WriteRows_PrintsAlignedLabelValuePairs()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.Summary.WriteRows(
            ("Name", "Example"),
            ("Items", TerminalSummary.FormatCount(2, "item(s)")));

        string output = console.Output;
        Assert.Contains("Name", output);
        Assert.Contains("Example", output);
        Assert.Contains("Items", output);
        Assert.Contains("2 item(s)", output);
    }

    [Fact]
    public void Summary_WriteRows_AlignsLocalizedLabelsByDisplayWidth()
    {
        TestConsole console = CreateConsole().SupportsAnsi(false);
        var ui = new TerminalUI(console, new AppInfo("Unity Assets Patcher", "dev"));

        ui.Summary.WriteRows(
            ("Mod", "Ridgeview"),
            ("目标", "1"));

        string[] lines = console.Output
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int englishValueColumn = GetDisplayWidth(lines[0][..lines[0].IndexOf("Ridgeview", StringComparison.Ordinal)]);
        int localizedValueColumn = GetDisplayWidth(lines[1][..lines[1].IndexOf("1", StringComparison.Ordinal)]);

        Assert.Equal(englishValueColumn, localizedValueColumn);
    }

    [Fact]
    public void ReadSelection_WhenDownWrapsPastLastChoice_ReturnsFirstChoice()
    {
        TestConsole console = CreateConsole();
        var renders = new List<(int SelectedIndex, bool Clear)>();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var prompt = new TerminalSelectionPrompt(console);

        int? selectedIndex = prompt.ReadSelection(
            optionCount: 2,
            initialSelectedIndex: 1,
            render: (index, clear) => renders.Add((index, clear)));

        Assert.Equal(0, selectedIndex);
        Assert.Equal([(1, true), (0, false)], renders);
    }

    [Fact]
    public void ReadSelection_WhenEscapeIsPressed_ReturnsNull()
    {
        TestConsole console = CreateConsole();
        console.Input.PushKey(ConsoleKey.Escape);
        var prompt = new TerminalSelectionPrompt(console);

        int? selectedIndex = prompt.ReadSelection(
            optionCount: 2,
            initialSelectedIndex: 0,
            render: (_, _) => { });

        Assert.Null(selectedIndex);
    }

    [Fact]
    public void ReadSelection_WhenAcceptKeyIsSpace_ReturnsSelectedChoice()
    {
        TestConsole console = CreateConsole();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        var prompt = new TerminalSelectionPrompt(console);

        int? selectedIndex = prompt.ReadSelection(
            optionCount: 2,
            initialSelectedIndex: 0,
            render: (_, _) => { },
            acceptKey: ConsoleKey.Spacebar);

        Assert.Equal(1, selectedIndex);
    }

    private static TestConsole CreateConsole()
    {
        return new TestConsole()
            .Interactive()
            .SupportsAnsi(true)
            .SupportsUnicode(false)
            .Width(120);
    }

    private static int GetDisplayWidth(string value)
    {
        return value.Sum(character => character is >= '\u1100' and <= '\u115f' or >= '\u2e80' and <= '\ua4cf'
            or >= '\uac00' and <= '\ud7a3' or >= '\uf900' and <= '\ufaff' or >= '\ufe10' and <= '\ufe19'
            or >= '\ufe30' and <= '\ufe6f' or >= '\uff00' and <= '\uff60' or >= '\uffe0' and <= '\uffe6'
            ? 2
            : 1);
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
