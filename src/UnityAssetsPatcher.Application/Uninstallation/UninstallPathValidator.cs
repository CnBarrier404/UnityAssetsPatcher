using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Uninstallation;

public static class UninstallPathValidator
{
    public static void ValidateInstallDirectory(
        IFileSystemOperations fileSystemOperations,
        string backupDirectory,
        string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        string fullBackupDirectory = fileSystemOperations.ResolveExistingDirectory(
            Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName));
        string fullInstallDirectory = fileSystemOperations.ResolveExistingDirectory(installDirectory);

        if (PathsEqual(fullInstallDirectory, fullBackupDirectory) ||
            !IsPathInsideDirectory(fullInstallDirectory, fullBackupDirectory))
        {
            throw new InvalidOperationException(
                $"Install directory must be inside the backup directory: {installDirectory}");
        }
    }

    public static UninstallResolvedPaths ResolveRecordPaths(
        IFileSystemOperations fileSystemOperations,
        string backupDirectory,
        string installDirectory,
        string gameDirectory,
        InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        ValidateInstallDirectory(fileSystemOperations, backupDirectory, installDirectory);

        string fullInstallDirectory = fileSystemOperations.ResolveExistingDirectory(installDirectory);
        string fullGameDirectory = fileSystemOperations.ResolveExistingDirectory(gameDirectory);

        var patchedFiles = record.PatchedFiles
            .Select(file => ResolvePatchedFile(fileSystemOperations, fullInstallDirectory, fullGameDirectory, file))
            .ToArray();

        var copiedFiles = record.CopiedFiles
            .Select(file => ResolveCopiedFile(fileSystemOperations, fullGameDirectory, file))
            .ToArray();

        return new UninstallResolvedPaths(fullGameDirectory, patchedFiles, copiedFiles);
    }

    private static UninstallResolvedPatchedFile ResolvePatchedFile(
        IFileSystemOperations fileSystemOperations,
        string fullInstallDirectory,
        string fullGameDirectory,
        InstallRecordPatchedFile file)
    {
        string backupPath = ResolveRelativePath(
            fileSystemOperations,
            fullInstallDirectory,
            file.BackupRelativePath,
            "backup path");
        string assetsFilePath = ResolveRelativePath(
            fileSystemOperations,
            fullGameDirectory,
            file.AssetsFileRelativePath,
            "assets file path");

        if (!FileNamesEqual(assetsFilePath, file.Target))
        {
            throw new InvalidOperationException(
                $"Patched assets file name must match target file name: {file.AssetsFileRelativePath}");
        }

        return new UninstallResolvedPatchedFile(
            file.Target,
            assetsFilePath,
            backupPath,
            file.InstalledFile,
            file.BackupFile);
    }

    private static UninstallResolvedCopiedFile ResolveCopiedFile(
        IFileSystemOperations fileSystemOperations,
        string fullGameDirectory,
        InstallRecordCopiedFile file)
    {
        string destinationPath = ResolveRelativePath(
            fileSystemOperations,
            fullGameDirectory,
            file.DestinationRelativePath,
            "payload destination path");

        if (!FileNamesEqual(destinationPath, file.Source))
        {
            throw new InvalidOperationException(
                $"Payload destination file name must match source file name: {file.DestinationRelativePath}");
        }

        return new UninstallResolvedCopiedFile(destinationPath, file.InstalledFile);
    }

    private static string ResolveRelativePath(
        IFileSystemOperations fileSystemOperations,
        string rootDirectory,
        string relativePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            Path.GetPathRoot(relativePath)?.Length > 0 ||
            ContainsNavigationSegment(relativePath))
        {
            throw new InvalidOperationException($"Invalid uninstall {description}: {relativePath}");
        }

        try
        {
            return fileSystemOperations.ResolveWithinDirectory(rootDirectory, relativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Uninstall {description} must be inside its trusted directory: {relativePath}", exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Invalid uninstall {description}: {relativePath}", exception);
        }
    }

    private static bool ContainsNavigationSegment(string path)
    {
        return path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }

    private static bool IsPathInsideDirectory(string fullPath, string fullDirectory)
    {
        string directory = EnsureTrailingDirectorySeparator(fullDirectory);

        return fullPath.StartsWith(directory, PathComparison);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            TrimTrailingDirectorySeparator(left),
            TrimTrailingDirectorySeparator(right),
            PathComparison);
    }

    private static bool FileNamesEqual(string leftPath, string rightPath)
    {
        return string.Equals(
            Path.GetFileName(leftPath),
            Path.GetFileName(rightPath),
            PathComparison);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        string trimmed = TrimTrailingDirectorySeparator(path);

        return trimmed + Path.DirectorySeparatorChar;
    }

    private static string TrimTrailingDirectorySeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed record UninstallResolvedPaths(
    string GameDirectory,
    IReadOnlyList<UninstallResolvedPatchedFile> PatchedFiles,
    IReadOnlyList<UninstallResolvedCopiedFile> CopiedFiles);

public sealed record UninstallResolvedPatchedFile(
    string Target,
    string AssetsFilePath,
    string BackupPath,
    FileIntegrity InstalledFile,
    FileIntegrity BackupFile);

public sealed record UninstallResolvedCopiedFile(
    string DestinationPath,
    FileIntegrity InstalledFile);
