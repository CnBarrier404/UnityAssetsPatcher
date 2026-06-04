using System.IO.Compression;

namespace UnityAssetsPatcher.Application.Installing;

public static class InstallZipPaths
{
    public static string NormalizeZipEntryPath(string source)
    {
        return source.Replace('\\', '/');
    }

    public static string GetPayloadFileName(string source)
    {
        string fileName = Path.GetFileName(source.Replace('/', Path.DirectorySeparatorChar));

        return string.IsNullOrWhiteSpace(fileName)
            ? throw new InvalidOperationException($"Install payload source must name a file: {source}")
            : fileName;
    }

    public static ZipArchiveEntry FindRequiredZipEntry(
        ZipArchive archive,
        string source,
        string zipFilePath)
    {
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.FullName, source, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return entries.Length switch
        {
            1 => entries[0],
            0 => throw new FileNotFoundException(
                $"Zip payload file not found: {source} in {zipFilePath}",
                source),
            _ => throw new InvalidOperationException($"Zip payload file matched multiple entries: {source}")
        };
    }

    public static void CopyZipEntry(ZipArchiveEntry entry, string destinationPath)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        string tempPath = Path.Combine(
            string.IsNullOrEmpty(destinationDirectory) ? Directory.GetCurrentDirectory() : destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (Stream sourceStream = entry.Open())
            using (FileStream outputStream = File.Create(tempPath))
            {
                sourceStream.CopyTo(outputStream);
            }

            File.Move(tempPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static string ResolvePathUnderDirectory(string rootDirectory, string relativePath)
    {
        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootWithSeparator = fullRootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullRootDirectory
            : fullRootDirectory + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Zip payload source cannot escape its extraction directory: {relativePath}");
        }

        return fullPath;
    }
}
