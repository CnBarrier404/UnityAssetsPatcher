namespace UnityAssetsPatcher.Application.IO;

public sealed class TrustedPathResolver
{
    private readonly IFileSystemOperations _fileSystemOperations;

    public TrustedPathResolver(IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        _fileSystemOperations = fileSystemOperations;
    }

    public string ResolveExistingDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = TrustedPath.NormalizeAbsolutePath(path);

        if (!TryGetAttributes(fullPath, out FileAttributes attributes) ||
            !attributes.HasFlag(FileAttributes.Directory))
        {
            throw new DirectoryNotFoundException($"The directory does not exist: '{fullPath}'.");
        }

        return fullPath;
    }

    public string ResolveWithinDirectory(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string root = ResolveExistingDirectory(rootDirectory);

        if (!TrustedPath.TryNormalizeRelativePath(relativePath, out string normalizedRelativePath))
        {
            throw new IOException($"The relative path is not trusted: '{relativePath}'.");
        }

        string targetPath = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));

        if (!TrustedPath.IsWithinRoot(targetPath, root))
        {
            throw new IOException($"The resolved path escapes the trusted root: '{targetPath}'.");
        }

        RejectReparsePointEscapes(root, targetPath);

        return targetPath;
    }

    private void RejectReparsePointEscapes(string root, string targetPath)
    {
        string? current = Path.GetDirectoryName(targetPath);

        while (current is not null &&
               TrustedPath.IsWithinRoot(current, root) &&
               !TrustedPath.PathsEqual(current, root))
        {
            if (TryGetAttributes(current, out FileAttributes attributes) &&
                attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException($"A path component below the trusted root is a reparse point: '{current}'.");
            }

            current = Path.GetDirectoryName(current);
        }

        if (TryGetAttributes(targetPath, out FileAttributes targetAttributes) &&
            targetAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"The target path is a reparse point: '{targetPath}'.");
        }
    }

    private bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = _fileSystemOperations.GetAttributes(path);

            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;

            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;

            return false;
        }
    }
}
