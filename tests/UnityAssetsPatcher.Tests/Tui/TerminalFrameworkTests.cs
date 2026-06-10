using Spectre.Console.Testing;
using UnityAssetsPatcher.Tui;
using Xunit;

namespace UnityAssetsPatcher.Tests.Tui;

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
    public void Renderer_ShowPageAndPrepareOutputArea_DelegatesLayoutRendering()
    {
        TestConsole console = CreateConsole().Height(10);
        var renderer = new TerminalRenderer(console);

        renderer.ShowPage("Install Mod", "Analyze the package first.");
        renderer.PrepareOutputArea();

        string output = console.Output;
        Assert.Contains("Unity Assets Patcher", output);
        Assert.Contains("Install Mod", output);
        Assert.Contains("Analyze the package first.", output);
        Assert.Contains("\u001b[s", output);
        Assert.Contains("\u001b[u", output);
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
