using UnityAssetsPatcher.Application.Backups;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Application.Uninstallation;

internal static class UninstallIntegrityInspector
{
    public static FileIntegrityStatus Inspect(string path, FileIntegrity expected)
    {
        if (!File.Exists(path))
        {
            return FileIntegrityStatus.Missing;
        }

        try
        {
            return expected.Matches(path)
                ? FileIntegrityStatus.Matches
                : FileIntegrityStatus.Modified;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileIntegrityStatus.Unreadable;
        }
    }

    public static void EnsureSafeToUninstall(UninstallResolvedPaths paths)
    {
        foreach (UninstallResolvedPatchedFile file in paths.PatchedFiles)
        {
            EnsureMatches(file.AssetsFilePath, file.InstalledFile, "assets file");
            EnsureMatches(file.BackupPath, file.BackupFile, "backup file");
        }

        foreach (UninstallResolvedCopiedFile file in paths.CopiedFiles)
        {
            FileIntegrityStatus status = Inspect(file.DestinationPath, file.InstalledFile);
            if (status is FileIntegrityStatus.Matches or FileIntegrityStatus.Missing)
            {
                continue;
            }

            throw CreateConflict(file.DestinationPath, "payload file", status);
        }
    }

    private static void EnsureMatches(string path, FileIntegrity expected, string kind)
    {
        FileIntegrityStatus status = Inspect(path, expected);
        if (status != FileIntegrityStatus.Matches)
        {
            throw CreateConflict(path, kind, status);
        }
    }

    private static Exception CreateConflict(string path, string kind, FileIntegrityStatus status)
    {
        return new InvalidOperationException(
            $"Cannot uninstall because the {kind} is {Describe(status)}: {path}");
    }

    private static string Describe(FileIntegrityStatus status) => status switch
    {
        FileIntegrityStatus.Missing => "missing",
        FileIntegrityStatus.Modified => "different from the installed file",
        FileIntegrityStatus.Unreadable => "unreadable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
