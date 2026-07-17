using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Uninstallation;

public static class UninstallPathValidator
{
    public static void ValidateInstallDirectory(string backupDirectory, string installDirectory)
    {
        string fullBackupDirectory = GetResolvedPath(
            Path.Combine(backupDirectory, BackupRepository.InstalledDirectoryName), "installed records directory");
        string fullInstallDirectory = GetResolvedPath(installDirectory, "install directory");

        if (PathsEqual(fullInstallDirectory, fullBackupDirectory) ||
            !IsPathInsideDirectory(fullInstallDirectory, fullBackupDirectory))
        {
            throw new InvalidOperationException(
                $"Install directory must be inside the backup directory: {installDirectory}");
        }
    }

    public static UninstallResolvedPaths ResolveRecordPaths(
        string backupDirectory,
        string installDirectory,
        string gameDirectory,
        InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        ValidateInstallDirectory(backupDirectory, installDirectory);

        string fullInstallDirectory = GetResolvedPath(installDirectory, "install directory");
        string fullGameDirectory = GetExistingDirectoryPath(gameDirectory, "game directory");

        var patchedFiles = record.PatchedFiles
            .Select(file => ResolvePatchedFile(fullInstallDirectory, fullGameDirectory, file))
            .ToArray();

        var copiedFiles = record.CopiedFiles
            .Select(file => ResolveCopiedFile(fullGameDirectory, file))
            .ToArray();

        return new UninstallResolvedPaths(fullGameDirectory, patchedFiles, copiedFiles);
    }

    private static UninstallResolvedPatchedFile ResolvePatchedFile(
        string fullInstallDirectory,
        string fullGameDirectory,
        InstallRecordPatchedFile file)
    {
        string backupPath = ResolveRelativePath(
            fullInstallDirectory,
            file.BackupRelativePath,
            "backup path");
        string assetsFilePath = ResolveRelativePath(
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
        string fullGameDirectory,
        InstallRecordCopiedFile file)
    {
        string destinationPath = ResolveRelativePath(
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

    private static string ResolveRelativePath(string rootDirectory, string relativePath, string description)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            Path.GetPathRoot(relativePath)?.Length > 0 ||
            ContainsNavigationSegment(relativePath))
        {
            throw new InvalidOperationException($"Invalid uninstall {description}: {relativePath}");
        }

        string resolvedPath = GetResolvedPath(Path.Combine(rootDirectory, relativePath), description);

        if (!IsPathInsideDirectory(resolvedPath, rootDirectory))
        {
            throw new InvalidOperationException(
                $"Uninstall {description} must be inside its trusted directory: {relativePath}");
        }

        return resolvedPath;
    }

    private static bool ContainsNavigationSegment(string path)
    {
        return path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }

    private static string GetExistingDirectoryPath(string path, string description)
    {
        string resolvedPath = GetResolvedPath(path, description);

        return Directory.Exists(resolvedPath)
            ? resolvedPath
            : throw new DirectoryNotFoundException($"Game directory not found: {resolvedPath}");
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

    private static string GetResolvedPath(string path, string description)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath) ??
                          throw new InvalidOperationException($"Cannot resolve uninstall {description}: {path}");
            string resolvedPath = root;
            string[] segments = fullPath[root.Length..]
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                resolvedPath = Path.Combine(resolvedPath, segment);

                FileSystemInfo? link = GetLink(resolvedPath);
                if (link?.LinkTarget is not null)
                {
                    resolvedPath = link.ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
                                   throw new InvalidOperationException(
                                       $"Cannot resolve uninstall {description}: {path}");
                }
            }

            return resolvedPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException($"Invalid uninstall {description}: {path}", exception);
        }
    }

    private static FileSystemInfo? GetLink(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        return File.Exists(path) ? new FileInfo(path) : null;
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
