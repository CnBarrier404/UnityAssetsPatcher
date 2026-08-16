namespace UnityAssetsPatcher.Application.IO;

using Domain.Integrity;

public static class FileSystemOperationsCompatibilityExtensions
{
    public static bool MatchesFile(
        this IFileSystemOperations fileSystemOperations,
        string path,
        FileIntegrity expected)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(expected);

        FileIntegrity actual = fileSystemOperations.ComputeFileIntegrity(path);

        return expected.Matches(actual);
    }

    public static string ResolveExistingDirectory(this IFileSystemOperations fileSystemOperations, string path)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        var resolver = new TrustedPathResolver(fileSystemOperations);

        return resolver.ResolveExistingDirectory(path);
    }

    public static string ResolveExistingFile(this IFileSystemOperations fileSystemOperations, string path)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = TrustedPath.NormalizeAbsolutePath(path);
        FileAttributes attributes = fileSystemOperations.GetAttributes(fullPath);

        if (attributes.HasFlag(FileAttributes.Directory))
        {
            throw new FileNotFoundException($"The file does not exist: '{fullPath}'.", fullPath);
        }

        return fullPath;
    }

    public static string ResolveWithinDirectory(
        this IFileSystemOperations fileSystemOperations,
        string rootDirectory,
        string relativePath)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        var resolver = new TrustedPathResolver(fileSystemOperations);

        return resolver.ResolveWithinDirectory(rootDirectory, relativePath);
    }

    public static bool PathsEqual(this IFileSystemOperations fileSystemOperations, string leftPath, string rightPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        return TrustedPath.PathsEqual(leftPath, rightPath);
    }

    public static bool IsPathWithinDirectory(
        this IFileSystemOperations fileSystemOperations,
        string path,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        return !TrustedPath.PathsEqual(path, directory) && TrustedPath.IsWithinRoot(path, directory);
    }

    public static void WriteFile(
        this IFileSystemOperations fileSystemOperations,
        string destinationPath,
        Action<Stream> writer)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        fileSystemOperations.WriteFileAtomically(destinationPath, FileDestinationMode.CreateOrReplace, writer);
    }

    public static void CopyFile(
        this IFileSystemOperations fileSystemOperations,
        string sourcePath,
        string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        fileSystemOperations.CopyFileAtomically(sourcePath, destinationPath, FileDestinationMode.CreateOrReplace);
    }

    public static void MoveFile(
        this IFileSystemOperations fileSystemOperations,
        string sourcePath,
        string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        fileSystemOperations.CopyFileAtomically(sourcePath, destinationPath, FileDestinationMode.CreateOrReplace);

        fileSystemOperations.DeleteFile(sourcePath);
    }

    public static void CreateDirectory(this IFileSystemOperations fileSystemOperations, string path)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        fileSystemOperations.EnsureDirectory(path);
    }

    public static void DeleteDirectory(this IFileSystemOperations fileSystemOperations, string path)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        fileSystemOperations.DeleteDirectoryTree(path);
    }
}
