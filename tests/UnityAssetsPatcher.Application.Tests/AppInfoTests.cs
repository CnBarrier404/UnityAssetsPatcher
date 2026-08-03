using Xunit;

namespace UnityAssetsPatcher.Application.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void FromVersion_WhenVersionContainsMetadata_RemovesMetadata()
    {
        AppInfo appInfo = AppInfo.FromVersion("Example Tool", "v1.2.3+sha.1234");

        Assert.Equal("Example Tool", appInfo.Name);
        Assert.Equal("v1.2.3", appInfo.DisplayVersion);
    }

    [Fact]
    public void FromVersion_WhenVersionDoesNotStartWithV_UsesDevVersion()
    {
        AppInfo appInfo = AppInfo.FromVersion("Example Tool", "1.2.3");

        Assert.Equal("dev", appInfo.DisplayVersion);
    }
}
