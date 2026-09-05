using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Domain.Integrity;

namespace UnityAssetsPatcher.Infrastructure.IO;

public sealed class FileSystemOperations : IFileSystemOperations
{
    private const int FileIntegrityBufferSize = 81920;

    private static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ILogger<FileSystemOperations> _logger;

    public FileSystemOperations(ILogger<FileSystemOperations> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public Stream OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        IOLog.OpeningFileForRead(_logger, fullPath);

        try
        {
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            IOLog.FileOpenedForRead(_logger, fullPath);

            return stream;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            IOLog.FileOpenForReadFailed(_logger, fullPath, exception);

            throw;
        }
    }

    public FileIntegrity ComputeFileIntegrity(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using Stream source = OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[FileIntegrityBufferSize];
        long length = 0;
        int bytesRead;

        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, bytesRead);
            length = checked(length + bytesRead);
        }

        byte[] digest = hash.GetHashAndReset();

        return new FileIntegrity(length, Convert.ToHexStringLower(digest));
    }

    public FileAttributes GetAttributes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        IOLog.GettingAttributes(_logger, fullPath);

        try
        {
            FileAttributes attributes = File.GetAttributes(fullPath);

            IOLog.AttributesRead(_logger, fullPath, attributes);

            return attributes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            IOLog.GetAttributesFailed(_logger, fullPath, exception);

            throw;
        }
    }

    public void WriteFileAtomically(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(writer);
        ValidateDestinationMode(mode);

        string fullDestinationPath = Path.GetFullPath(destinationPath);

        IOLog.WritingFile(_logger, fullDestinationPath, mode);

        WriteFileCore(fullDestinationPath, mode, writer);

        IOLog.FileWritten(_logger, fullDestinationPath, mode);
    }

    public void CopyFileAtomically(string sourcePath, string destinationPath, FileDestinationMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ValidateDestinationMode(mode);

        string fullSourcePath = Path.GetFullPath(sourcePath);

        string fullDestinationPath = Path.GetFullPath(destinationPath);

        IOLog.CopyingFile(_logger, fullSourcePath, fullDestinationPath, mode);

        WriteFileCore(fullDestinationPath, mode, destinationStream =>
        {
            using FileStream sourceStream = new(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            sourceStream.CopyTo(destinationStream);
        });

        IOLog.FileCopied(_logger, fullSourcePath, fullDestinationPath, mode);
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

        IOLog.DeletingFile(_logger, fullPath);

        if (attributes.HasFlag(FileAttributes.Directory))
        {
            Directory.Delete(fullPath);
        }
        else
        {
            File.Delete(fullPath);
        }

        IOLog.FileDeleted(_logger, fullPath);
    }

    public void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        IOLog.EnsuringDirectory(_logger, fullPath);

        Directory.CreateDirectory(fullPath);

        IOLog.DirectoryReady(_logger, fullPath);
    }

    public void MoveDirectory(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        string fullSourcePath = Path.GetFullPath(sourcePath);

        string fullDestinationPath = Path.GetFullPath(destinationPath);

        if (PathComparer.Equals(fullSourcePath, fullDestinationPath))
        {
            throw new ArgumentException("The source and destination must be different paths.", nameof(destinationPath));
        }

        IOLog.MovingDirectory(_logger, fullSourcePath, fullDestinationPath);

        Directory.Move(fullSourcePath, fullDestinationPath);

        IOLog.DirectoryMoved(_logger, fullSourcePath, fullDestinationPath);
    }

    public void DeleteDirectoryTree(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        FileAttributes attributes = File.GetAttributes(fullPath);

        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            throw new IOException($"The path must be a directory or directory link: '{fullPath}'.");
        }

        IOLog.DeletingDirectoryTree(_logger, fullPath);

        DeleteDirectoryTreeCore(fullPath, attributes);

        IOLog.DirectoryTreeDeleted(_logger, fullPath);
    }

    private void WriteFileCore(string destinationPath, FileDestinationMode mode, Action<Stream> writer)
    {
        string temporaryPath = CreateSiblingPath(destinationPath, "tmp");
        bool temporaryFileCreated = false;

        try
        {
            FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            temporaryFileCreated = true;
            Exception? writeFailure = null;
            try
            {
                writer(stream);

                stream.Flush(true);
            }
            catch (Exception failure)
            {
                writeFailure = failure;
                throw;
            }
            finally
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception cleanupFailure) when (writeFailure is not null)
                {
                    throw new AggregateException(writeFailure, cleanupFailure);
                }
            }

            CommitFile(temporaryPath, destinationPath, mode);
        }
        catch (Exception failure)
        {
            if (temporaryFileCreated)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(failure, cleanupFailure);
                }
            }

            throw;
        }
    }

    private void CommitFile(string stagedPath, string destinationPath, FileDestinationMode mode)
    {
        string stagedDirectory = Path.GetDirectoryName(stagedPath) ??
                                 throw new IOException($"Cannot resolve staged file directory: {stagedPath}");

        string destinationDirectory = Path.GetDirectoryName(destinationPath) ??
                                      throw new IOException($"Cannot resolve destination directory: {destinationPath}");

        if (!PathComparer.Equals(stagedDirectory, destinationDirectory))
        {
            throw new IOException("The staged file and destination must be in the same directory.");
        }

        if (PathComparer.Equals(stagedPath, destinationPath))
        {
            throw new IOException("The staged file and destination must be different paths.");
        }

        FileAttributes stagedAttributes = File.GetAttributes(stagedPath);

        if (stagedAttributes.HasFlag(FileAttributes.Directory) || stagedAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("The staged path must be a regular file and must not be a reparse point.");
        }

        if (!TryGetAttributes(destinationPath, out FileAttributes destinationAttributes))
        {
            if (mode == FileDestinationMode.ReplaceExisting)
            {
                throw new FileNotFoundException(
                    $"The destination file does not exist: '{destinationPath}'.",
                    destinationPath);
            }

            File.Move(stagedPath, destinationPath, false);

            return;
        }

        if (mode == FileDestinationMode.CreateNew)
        {
            throw new IOException($"The destination path already exists: '{destinationPath}'.");
        }

        if (destinationAttributes.HasFlag(FileAttributes.Directory) ||
            destinationAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"The destination must be a regular file: '{destinationPath}'.");
        }

        string recoveryPath = CreateSiblingPath(destinationPath, "recovery");

        File.Replace(stagedPath, destinationPath, recoveryPath, false);

        File.Delete(recoveryPath);
    }

    private static void ValidateDestinationMode(FileDestinationMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported file destination mode.");
        }
    }

    private static void DeleteDirectoryTreeCore(string directoryPath, FileAttributes attributes)
    {
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            Directory.Delete(directoryPath);

            return;
        }

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileAttributes entryAttributes = File.GetAttributes(entryPath);

            if (entryAttributes.HasFlag(FileAttributes.Directory))
            {
                DeleteDirectoryTreeCore(entryPath, entryAttributes);
            }
            else
            {
                File.Delete(entryPath);
            }
        }

        Directory.Delete(directoryPath);
    }

    private static string CreateSiblingPath(string destinationPath, string suffix)
    {
        string directory = Path.GetDirectoryName(destinationPath) ??
                           throw new IOException($"Cannot resolve destination directory: {destinationPath}");

        string fileName = Path.GetFileName(destinationPath);

        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.{suffix}");
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
