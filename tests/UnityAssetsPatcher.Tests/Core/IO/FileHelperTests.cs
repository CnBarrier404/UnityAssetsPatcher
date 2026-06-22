using UnityAssetsPatcher.Core.IO;
using Xunit;

namespace UnityAssetsPatcher.Tests.Core.IO;

public sealed class FileHelperTests
{
    [Fact]
    public void SafeMoveFile_WhenSourceIsMissingAndDestinationIsRegularFile_PreservesDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string destinationPath = Path.Combine(root, "destination.txt");
        string missingSourcePath = Path.Combine(root, "missing-source.txt");
        File.WriteAllText(destinationPath, "destination");

        try
        {
            var exception = Assert.Throws<FileNotFoundException>(() =>
                FileHelper.SafeMoveFile(missingSourcePath, destinationPath, overwrite: true));

            Assert.Equal(missingSourcePath, exception.FileName);
            Assert.True(File.Exists(destinationPath));
            Assert.Equal("destination", File.ReadAllText(destinationPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void SafeMoveFile_WhenSourceIsMissingAndDestinationIsReparsePoint_PreservesDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string targetPath = Path.Combine(root, "target.txt");
        string linkPath = Path.Combine(root, "link.txt");
        string missingSourcePath = Path.Combine(root, "missing-source.txt");
        File.WriteAllText(targetPath, "target");

        try
        {
            if (!TryCreateFileSymbolicLink(linkPath, targetPath, out string? skipReason))
            {
                Assert.Skip(skipReason!);
            }

            var exception = Assert.Throws<FileNotFoundException>(() =>
                FileHelper.SafeMoveFile(missingSourcePath, linkPath, overwrite: true));

            Assert.Equal(missingSourcePath, exception.FileName);
            Assert.True(File.Exists(linkPath));
            Assert.True(File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint));
            Assert.Equal("target", File.ReadAllText(linkPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath, out string? skipReason)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            skipReason = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skipReason = $"Cannot create file symbolic link in this environment: {exception.Message}";
            return false;
        }
    }
}
