using System.Globalization;
using Spectre.Console.Testing;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.TUI.Framework;
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
    public void ReturnToMenu_WhenWaitForKeyIsFalse_ExpressesImmediateReturn()
    {
        TerminalPageResult result = TerminalPageResult.ReturnToMenu(false);

        Assert.Equal(TerminalPageAction.ReturnToMenu, result.Action);
        Assert.False(result.WaitForKey);
    }

    [Fact]
    public void PageContract_RunReturnsExplicitPageResult()
    {
        Assert.Equal(
            typeof(TerminalPageResult),
            typeof(ITerminalPage).GetMethod(nameof(ITerminalPage.Run))?.ReturnType);
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
    public void Lists_WriteChoiceList_MarksSelectedChoice()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.List.WriteChoiceList(["First action", "Second action"], selectedIndex: 1);

        Assert.Contains("  First action", console.Output);
        Assert.Contains("> Second action", console.Output);
    }

    [Fact]
    public void Lists_WriteDescribedChoiceList_AlignsLabelAndDescription()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.List.WriteDescribedChoiceList(
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
    public void Lists_WriteToggleList_UsesFixedSpacingForLocalizedLabels()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.List.WriteToggleList(
            [
                new TerminalToggleDisplay("详细日志", "显示详细安装预览日志。", false),
                new TerminalToggleDisplay("安装耗时详情", "显示各阶段耗时。", false),
            ],
            selectedIndex: 0);

        string line = console.Output
            .ReplaceLineEndings("\n")
            .Split('\n')
            .First(line => line.Contains("详细日志", StringComparison.Ordinal));
        int labelEnd = line.IndexOf("详细日志", StringComparison.Ordinal) + "详细日志".Length;
        int descriptionStart = line.IndexOf("显示详细", StringComparison.Ordinal);

        Assert.InRange(descriptionStart - labelEnd, 11, 16);
    }

    [Fact]
    public void Lists_WriteToggleList_WrapsLongLabelsAndDescriptionsToConsoleWidth()
    {
        TestConsole console = CreateConsole().Width(42);
        var ui = new TerminalUI(console);

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

        Assert.True(lines.Length > 1, console.Output);
        Assert.All(lines, line => Assert.True(GetDisplayWidth(line) <= 42, line));
    }

    [Fact]
    public void Summary_WriteRows_PrintsAlignedLabelValuePairs()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

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
        var ui = new TerminalUI(console);

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
    public void Tables_WritePlainTable_PrintsEscapedHeadersAndStyledCells()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.Table.WritePlainTable(
            [
                new TerminalTableColumn("Target"),
                new TerminalTableColumn("Operations"),
                new TerminalTableColumn("Path"),
            ],
            [
                [
                    new TerminalTableCell("Level[7]"),
                    new TerminalTableCell("4 changed, 72 skipped"),
                    new TerminalTableCell(@"E:\Steam\Game_Data\level7", "grey"),
                ],
            ]);

        string output = console.Output;
        Assert.Contains("Target", output);
        Assert.Contains("Operations", output);
        Assert.Contains("Path", output);
        Assert.Contains("Level[7]", output);
        Assert.Contains("4 changed, 72 skipped", output);
        Assert.Contains(@"E:\Steam\Game_Data\level7", output);
        Assert.DoesNotContain("|", output);
    }

    [Fact]
    public void ReadChoice_WhenSelectionIsAccepted_ReturnsSelectedChoice()
    {
        TestConsole console = CreateConsole();
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        var ui = new TerminalUI(console);
        var prompts = new TerminalPrompts(console, ui.Text);

        string choice = prompts.ReadChoice(
            ["First action", "Second action"],
            cancelChoice: "__cancel",
            render: (selectedIndex, _) => ui.List.WriteChoiceList(
                ["First action", "Second action"],
                selectedIndex));

        Assert.Equal("Second action", choice);
        Assert.Contains("> Second action", console.Output);
    }

    [Fact]
    public void ReadChoice_WhenEscapeIsPressed_ReturnsCancelChoice()
    {
        TestConsole console = CreateConsole();
        console.Input.PushKey(ConsoleKey.Escape);
        var ui = new TerminalUI(console);
        var prompts = new TerminalPrompts(console, ui.Text);

        string choice = prompts.ReadChoice(
            ["First action", "Second action"],
            cancelChoice: "__cancel",
            render: (selectedIndex, _) => ui.List.WriteChoiceList(
                ["First action", "Second action"],
                selectedIndex));

        Assert.Equal("__cancel", choice);
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
}
