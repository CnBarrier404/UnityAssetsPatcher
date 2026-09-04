using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class PlaceholderTests
{
    [Fact]
    public void TUIAssembly_WhenLoaded_HasExpectedName()
    {
        string? assemblyName = typeof(TerminalApp).Assembly.GetName().Name;

        Console.WriteLine("Placeholder test: Ciallo～(∠・ω< )⌒★");

        Assert.Equal("UnityAssetsPatcher.TUI", assemblyName);
    }
}
