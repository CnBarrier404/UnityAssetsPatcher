using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class FileSystemOperations : IFileSystemOperations
{
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ILogger<FileSystemOperations> _logger;

    public FileSystemOperations(ILogger<FileSystemOperations>? logger = null)
    {
        _logger = logger ?? NullLogger<FileSystemOperations>.Instance;
    }

    public string ResolveExistingDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string resolved = ResolveExistingLinks(path);

        return !Directory.Exists(resolved)
            ? throw new DirectoryNotFoundException($"Directory not found: {resolved}")
            : NormalizeDirectory(resolved);
    }

    public string ResolveExistingFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string resolved = ResolveExistingLinks(path);

        return !File.Exists(resolved)
            ? throw new FileNotFoundException($"File not found: {resolved}", resolved)
            : Path.GetFullPath(resolved);
    }

    public string ResolveWithinDirectory(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            Path.GetPathRoot(relativePath)?.Length > 0 || ContainsNavigationSegment(relativePath))
        {
            throw new InvalidOperationException($"Relative path is not trusted: {relativePath}");
        }

        string root = ResolveExistingDirectory(rootDirectory);
        string resolved = ResolveExistingLinks(Path.Combine(root, relativePath));

        return !IsPathWithinDirectory(resolved, root)
            ? throw new InvalidOperationException($"Path escapes the trusted directory: {relativePath}")
            : Path.GetFullPath(resolved);
    }

    public bool PathsEqual(string leftPath, string rightPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leftPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightPath);

        return PathComparer.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(leftPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(rightPath)));
    }

    public bool IsPathWithinDirectory(string path, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string fullPath = Path.GetFullPath(path);
        string fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string prefix = Path.EndsInDirectorySeparator(fullDirectory)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(prefix, PathComparison);
    }

    public void WriteFile(string destinationPath, Action<Stream> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string temporaryPath = CreateTemporarySibling(destinationPath);

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writer(stream);
                stream.Flush(flushToDisk: true);
            }

            CommitFile(temporaryPath, destinationPath);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);

            throw;
        }
    }

    public void CopyFile(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        WriteFile(destinationPath, destinationStream =>
        {
            using FileStream sourceStream = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            sourceStream.CopyTo(destinationStream);
        });
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        CommitFile(sourcePath, destinationPath);
    }

    public void DeleteFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        FileAttributes attributes = File.GetAttributes(fullPath);

        if (attributes.HasFlag(FileAttributes.Directory) && !attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"The path must be a file or file-system link: '{fullPath}'.");
        }

        if (attributes.HasFlag(FileAttributes.Directory))
        {
            Directory.Delete(fullPath);
        }
        else
        {
            File.Delete(fullPath);
        }
    }

    public void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        EnsureExistingPathComponentsAreDirectories(fullPath);
        Directory.CreateDirectory(fullPath);
        EnsureRealDirectory(fullPath, "The created path");
    }

    public void MoveDirectory(string sourcePath, string destinationPath)
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

    public void DeleteDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        EnsureExistingPathComponentsAreDirectories(fullPath);
        EnsureRealDirectory(fullPath, "The directory to delete");
        EnsureTreeContainsNoReparsePoints(fullPath);
        DeleteContents(fullPath);
        Directory.Delete(fullPath);
    }

    private static string ResolveExistingLinks(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ??
                      throw new InvalidOperationException($"Cannot resolve path: {path}");
        string resolved = root;

        foreach (string segment in fullPath[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            FileSystemInfo? info = GetExistingEntry(resolved);

            if (info?.LinkTarget is not null)
            {
                resolved = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
                           throw new InvalidOperationException($"Cannot resolve path: {path}");
            }
        }

        return Path.GetFullPath(resolved);
    }

    private static FileSystemInfo? GetExistingEntry(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        return File.Exists(path) ? new FileInfo(path) : null;
    }

    private static bool ContainsNavigationSegment(string path)
    {
        return path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static void CommitFile(string stagedPath, string destinationPath)
    {
        string stagedFullPath = Path.GetFullPath(stagedPath);
        string destinationFullPath = Path.GetFullPath(destinationPath);
        string stagedDirectory = Path.GetDirectoryName(stagedFullPath) ??
                                 throw new IOException($"Cannot resolve staged file directory: {stagedPath}");
        string destinationDirectory = Path.GetDirectoryName(destinationFullPath) ??
                                      throw new IOException($"Cannot resolve destination directory: {destinationPath}");

        if (!PathComparer.Equals(stagedDirectory, destinationDirectory))
        {
            throw new IOException("The staged file and destination must be in the same directory.");
        }

        if (PathComparer.Equals(stagedFullPath, destinationFullPath))
        {
            throw new IOException("The source and destination must be different paths.");
        }

        FileAttributes stagedAttributes = File.GetAttributes(stagedFullPath);

        if (stagedAttributes.HasFlag(FileAttributes.Directory) || stagedAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("The staged path must be a regular file and must not be a reparse point.");
        }

        ReplaceDestination(stagedFullPath, destinationFullPath);
    }

    private static void ReplaceDestination(string stagedPath, string destinationPath)
    {
        if (!TryGetAttributes(destinationPath, out FileAttributes destinationAttributes))
        {
            File.Move(stagedPath, destinationPath, overwrite: false);

            return;
        }

        if (destinationAttributes.HasFlag(FileAttributes.Directory) &&
            !destinationAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"The destination must be a file or file-system link: '{destinationPath}'.");
        }

        string recoveryPath = MoveToRecoveryPath(destinationPath);

        try
        {
            File.Move(stagedPath, destinationPath, overwrite: false);
        }
        catch (Exception commitException)
        {
            RestoreDestination(recoveryPath, destinationPath, commitException);

            throw;
        }

        DeleteRecoveryPath(recoveryPath, destinationAttributes.HasFlag(FileAttributes.Directory));
    }

    private static void RestoreDestination(string recoveryPath, string destinationPath, Exception commitException)
    {
        try
        {
            File.Move(recoveryPath, destinationPath, overwrite: false);
        }
        catch (Exception recoveryException)
        {
            throw new IOException(
                $"File commit failed and the original destination could not be restored. Recovery file: '{recoveryPath}'.",
                new AggregateException(commitException, recoveryException));
        }
    }

    private static void DeleteRecoveryPath(string recoveryPath, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.Delete(recoveryPath);
            }
            else
            {
                File.Delete(recoveryPath);
            }
        }
        catch (Exception cleanupException)
        {
            throw new IOException(
                $"The destination was replaced, but the old destination could not be removed. Recovery file: '{recoveryPath}'.",
                cleanupException);
        }
    }

    private static string CreateTemporarySibling(string destinationPath)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullPath) ??
                           throw new IOException($"Cannot resolve destination directory: {destinationPath}");
        string fileName = Path.GetFileName(fullPath);

        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static string MoveToRecoveryPath(string destinationPath)
    {
        string directory = Path.GetDirectoryName(destinationPath)!;
        string fileName = Path.GetFileName(destinationPath);

        while (true)
        {
            string candidate = Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.recovery");

            try
            {
                File.Move(destinationPath, candidate, overwrite: false);

                return candidate;
            }
            catch (IOException) when (TryGetAttributes(candidate, out _)) { }
        }
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

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete the temporary file after a failed write: {TemporaryPath}",
                path);
        }
    }
}
