using Spectre.Console.Testing;
using UnityAssetsPatcher.TUI;
using UnityAssetsPatcher.TUI.Framework;
using Xunit;

namespace UnityAssetsPatcher.Tests.TUI;

public sealed class TerminalFrameworkTests
{
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
        var ui = new TerminalUI(console);

        ui.Layout.ShowPage("Task Runner", "Choose an action first.");
        ui.Layout.PrepareOutputArea();

        string output = console.Output;
        Assert.Contains("Unity Assets Patcher", output);
        Assert.Contains("Task Runner", output);
        Assert.Contains("Choose an action first.", output);
        Assert.Contains("\u001b[s", output);
        Assert.Contains("\u001b[u", output);
    }

    [Fact]
    public void Lists_WriteChoiceList_MarksSelectedChoice()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.Lists.WriteChoiceList(["First action", "Second action"], selectedIndex: 1);

        Assert.Contains("  First action", console.Output);
        Assert.Contains("> Second action", console.Output);
    }

    [Fact]
    public void Lists_WriteDescribedChoiceList_AlignsLabelAndDescription()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.Lists.WriteDescribedChoiceList(
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
    public void Summary_WriteRows_PrintsAlignedLabelValuePairs()
    {
        TestConsole console = CreateConsole();
        var ui = new TerminalUI(console);

        ui.Summary.WriteRows(
            ("Name", "Example"),
            ("Items", ui.Summary.FormatCount(2, "item")));

        string output = console.Output;
        Assert.Contains("Name", output);
        Assert.Contains("Example", output);
        Assert.Contains("Items", output);
        Assert.Contains("2 items", output);
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
            render: (selectedIndex, _) => ui.Lists.WriteChoiceList(
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
            render: (selectedIndex, _) => ui.Lists.WriteChoiceList(
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
}
