using UnityAssetsPatcher.Application.Backups;

namespace UnityAssetsPatcher.Application.Uninstallation;

public static class UninstallPathValidator
{
    public static void ValidateInstallDirectory(string backupDirectory, string installDirectory)
    {
        string fullBackupDirectory = GetResolvedPath(backupDirectory, "backup directory");
        string fullInstallDirectory = GetResolvedPath(installDirectory, "install directory");

        if (PathsEqual(fullInstallDirectory, fullBackupDirectory) ||
            !IsPathInsideDirectory(fullInstallDirectory, fullBackupDirectory))
        {
            throw new InvalidOperationException(
                $"Install directory must be inside the backup directory: {installDirectory}");
        }
    }

    public static void ValidateRecordPaths(
        string backupDirectory,
        string installDirectory,
        InstallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ValidateInstallDirectory(backupDirectory, installDirectory);

        string fullInstallDirectory = GetResolvedPath(installDirectory, "install directory");
        string fullGameDirectory = GetResolvedPath(record.GameDirectory, "game directory");

        foreach (InstallRecordPatchedFile file in record.PatchedFiles)
        {
            ValidatePatchedFile(fullInstallDirectory, fullGameDirectory, file);
        }

        foreach (InstallRecordCopiedFile file in record.CopiedFiles)
        {
            ValidateCopiedFile(fullGameDirectory, file);
        }
    }

    private static void ValidatePatchedFile(
        string fullInstallDirectory,
        string fullGameDirectory,
        InstallRecordPatchedFile file)
    {
        string fullBackupPath = GetResolvedPath(file.BackupPath, "backup path");
        string fullAssetsFilePath = GetResolvedPath(file.AssetsFilePath, "assets file path");

        if (!IsPathInsideDirectory(fullBackupPath, fullInstallDirectory))
        {
            throw new InvalidOperationException(
                $"Backup path must be inside the install directory: {file.BackupPath}");
        }

        if (!IsPathInsideDirectory(fullAssetsFilePath, fullGameDirectory))
        {
            throw new InvalidOperationException(
                $"Assets file path must be inside the game directory: {file.AssetsFilePath}");
        }

        if (!FileNamesEqual(file.AssetsFilePath, file.Target))
        {
            throw new InvalidOperationException(
                $"Patched assets file name must match target file name: {file.AssetsFilePath}");
        }
    }

    private static void ValidateCopiedFile(string fullGameDirectory, InstallRecordCopiedFile file)
    {
        string fullDestinationPath = GetResolvedPath(file.DestinationPath, "payload destination path");

        if (!IsPathInsideDirectory(fullDestinationPath, fullGameDirectory))
        {
            throw new InvalidOperationException(
                $"Payload destination path must be inside the game directory: {file.DestinationPath}");
        }

        if (!FileNamesEqual(file.DestinationPath, file.Source))
        {
            throw new InvalidOperationException(
                $"Payload destination file name must match source file name: {file.DestinationPath}");
        }
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
