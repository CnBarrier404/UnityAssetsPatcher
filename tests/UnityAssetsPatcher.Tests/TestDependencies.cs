using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.Tests;

public static class TestDependencies
{
    public static IFileSystemOperations FileSystemOperations { get; } = new FileSystemOperations();
}
