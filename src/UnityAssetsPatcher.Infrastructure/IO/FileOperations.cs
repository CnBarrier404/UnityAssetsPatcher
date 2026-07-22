using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class FileOperations : IFileOperations
{
    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ILogger<FileOperations> _logger;

    public FileOperations(ILogger<FileOperations>? logger = null)
    {
        _logger = logger ?? NullLogger<FileOperations>.Instance;
    }

    public void Write(string destinationPath, Action<Stream> writer)
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

            Commit(temporaryPath, destinationPath);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);

            throw;
        }
    }

    public void Copy(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        Write(destinationPath, destinationStream =>
        {
            using FileStream sourceStream = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            sourceStream.CopyTo(destinationStream);
        });
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        Commit(sourcePath, destinationPath);
    }

    public void Delete(string path)
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

    private static void Commit(string stagedPath, string destinationPath)
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
