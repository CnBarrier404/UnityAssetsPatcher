using UnityAssetsPatcher.Infrastructure.IO;

namespace UnityAssetsPatcher.Tests;

public static class TestDependencies
{
    public static IFileOperations FileOperations { get; } = new FileOperations();
    public static IDirectoryOperations DirectoryOperations { get; } = new DirectoryOperations();
}
