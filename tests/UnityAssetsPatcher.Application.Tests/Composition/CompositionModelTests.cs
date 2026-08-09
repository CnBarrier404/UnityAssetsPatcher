using UnityAssetsPatcher.Application.Composition;
using UnityAssetsPatcher.Application.Repository;
using Xunit;

namespace UnityAssetsPatcher.Application.Tests.Composition;

public sealed class CompositionModelTests
{
    [Fact]
    public void CompositionRequest_WhenFileKindsShareTargetPath_ThrowsArgumentException()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "composition-model"));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRequest(
            root,
            Path.Combine(root, "working"),
            [],
            null,
            [
                new CompositionFileTarget(RepositoryFileKind.Assets, "Game_Data/sharedassets0.assets"),
                new CompositionFileTarget(RepositoryFileKind.Payload, "Game_Data/sharedassets0.assets"),
            ]));

        Assert.Equal("files", exception.ParamName);
    }
}
