using UnityAssetsPatcher.Core;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core;

public sealed class AppInfoTests
{
    [Fact]
    public void FromAssembly_UsesInformationalVersionWithoutMetadata()
    {
        AppInfo appInfo = AppInfo.FromAssembly("Example Tool", typeof(AppInfoTests).Assembly);

        Assert.Equal("Example Tool", appInfo.Name);
        Assert.DoesNotContain('+', appInfo.DisplayVersion);
    }

    [Fact]
    public void FromVersion_WhenVersionDoesNotStartWithV_UsesDevVersion()
    {
        AppInfo appInfo = AppInfo.FromVersion("Example Tool", "1.2.3");

        Assert.Equal("dev", appInfo.DisplayVersion);
    }
}
