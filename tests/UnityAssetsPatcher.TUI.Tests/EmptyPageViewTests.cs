using Terminal.Gui.Input;
using UnityAssetsPatcher.TUI.Pages;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class EmptyPageViewTests
{
    [Fact]
    public void EmptyPageView_WhenBackButtonAccepted_ReturnsToMainMenu()
    {
        int returnCount = 0;
        using EmptyPageView page = new("Install Mod", "Return to main menu", () => returnCount++);

        page.BackButton.InvokeCommand(Command.Accept);

        Assert.Equal(1, returnCount);
    }

    [Fact]
    public void EmptyPageView_WhenEscapePressed_ReturnsToMainMenu()
    {
        int returnCount = 0;
        using EmptyPageView page = new("Install Mod", "Return to main menu", () => returnCount++);

        page.NewKeyDownEvent(Key.Esc);

        Assert.Equal(1, returnCount);
    }
}
