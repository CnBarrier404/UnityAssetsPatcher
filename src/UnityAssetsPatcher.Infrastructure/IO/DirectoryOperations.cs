using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class DirectoryOperations : IDirectoryOperations
{
    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public void Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        EnsureExistingPathComponentsAreDirectories(fullPath);
        Directory.CreateDirectory(fullPath);
        EnsureRealDirectory(fullPath, "The created path");
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string sourceFullPath = Path.GetFullPath(sourcePath);
        string destinationFullPath = Path.GetFullPath(destinationPath);

        if (PathComparer.Equals(sourceFullPath, destinationFullPath))
        {
            throw new ArgumentException("The source and destination must be different paths.", nameof(destinationPath));
        }

        EnsureExistingPathComponentsAreDirectories(sourceFullPath);
        EnsureRealDirectory(sourceFullPath, "The source path");
        EnsureExistingPathComponentsAreDirectories(destinationFullPath);

        if (TryGetAttributes(destinationFullPath, out _))
        {
            throw new IOException($"The destination directory already exists: '{destinationFullPath}'.");
        }

        Directory.Move(sourceFullPath, destinationFullPath);
    }

    public void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        EnsureExistingPathComponentsAreDirectories(fullPath);
        EnsureRealDirectory(fullPath, "The directory to delete");
        EnsureTreeContainsNoReparsePoints(fullPath);
        DeleteContents(fullPath);
        Directory.Delete(fullPath);
    }

    private static void EnsureTreeContainsNoReparsePoints(string directoryPath)
    {
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileAttributes attributes = File.GetAttributes(entryPath);

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException(entryPath);
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                EnsureTreeContainsNoReparsePoints(entryPath);
            }
        }
    }

    private static void DeleteContents(string directoryPath)
    {
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileAttributes attributes = File.GetAttributes(entryPath);

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException(entryPath);
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                DeleteContents(entryPath);
                Directory.Delete(entryPath);
            }
            else
            {
                File.Delete(entryPath);
            }
        }
    }

    private static void EnsureExistingPathComponentsAreDirectories(string fullPath)
    {
        string root = Path.GetPathRoot(fullPath) ??
                      throw new ArgumentException($"Cannot resolve path root: '{fullPath}'.", nameof(fullPath));
        string currentPath = root;

        foreach (string segment in fullPath[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);

            if (!TryGetAttributes(currentPath, out FileAttributes attributes))
            {
                return;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException(currentPath);
            }

            if (!attributes.HasFlag(FileAttributes.Directory))
            {
                throw new IOException($"The directory path contains a file: '{currentPath}'.");
            }
        }
    }

    private static void EnsureRealDirectory(string path, string description)
    {
        FileAttributes attributes = File.GetAttributes(path);

        if (attributes.HasFlag(FileAttributes.Directory) && !attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException(path);
        }

        throw new IOException($"{description} must be a directory: '{path}'.");
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);

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
