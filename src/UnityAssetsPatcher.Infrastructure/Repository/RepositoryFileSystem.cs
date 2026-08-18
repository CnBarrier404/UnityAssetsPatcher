using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.Repository;

internal sealed class RepositoryFileSystem
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly TrustedPathResolver _pathResolver;

    public RepositoryFileSystem(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
        _pathResolver = new TrustedPathResolver(fileSystemOperations);
    }

    public static string NormalizeIdentifier(string value, string parameterName)
    {
        if (!TrustedPath.TryNormalizeRelativePath(value, out string normalizedPath) ||
            normalizedPath.IndexOfAny(['\\', '/', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException($"The identifier must be a single path segment: '{value}'.", parameterName);
        }

        return normalizedPath;
    }

    public void EnsureRegularFile(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a regular file: {path}");
        }
    }

    public void EnsureRealDirectory(string path, string description)
    {
        FileAttributes attributes = _fileSystemOperations.GetAttributes(path);

        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"{description} must be a real directory: {path}");
        }
    }

    public bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }

    public string ResolveExistingDirectory(string path)
    {
        return _pathResolver.ResolveExistingDirectory(path);
    }

    public string ResolveWithinDirectory(string rootDirectory, string relativePath)
    {
        return _pathResolver.ResolveWithinDirectory(rootDirectory, relativePath);
    }

    public string ResolvePreparedTransactionChild(
        string transactionDirectory,
        string path,
        string description)
    {
        EnsureRealDirectory(transactionDirectory, "Transaction directory");

        string fullPath = TrustedPath.NormalizeAbsolutePath(path);

        if (TrustedPath.PathsEqual(fullPath, transactionDirectory) ||
            !TrustedPath.IsWithinRoot(fullPath, transactionDirectory))
        {
            throw new InvalidOperationException($"{description} directory is outside the active transaction.");
        }

        string relativePath = Path.GetRelativePath(transactionDirectory, fullPath);

        return ResolveWithinDirectory(transactionDirectory, relativePath);
    }

    public string ResolveExistingTransactionChild(
        string transactionDirectory,
        string path,
        string description)
    {
        string resolvedPath = ResolvePreparedTransactionChild(transactionDirectory, path, description);
        EnsureRealDirectory(resolvedPath, $"{description} directory");

        return resolvedPath;
    }
}
