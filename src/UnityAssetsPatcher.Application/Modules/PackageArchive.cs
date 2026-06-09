using System.IO.Compression;

namespace UnityAssetsPatcher.Application.Modules;

public static class PackageArchive
{
    public static string NormalizeEntryPath(string source)
    {
        return source.Replace('\\', '/');
    }

    public static string GetFileName(string source)
    {
        string fileName = Path.GetFileName(source.Replace('/', Path.DirectorySeparatorChar));

        return string.IsNullOrWhiteSpace(fileName)
            ? throw new InvalidOperationException($"Payload source must name a file: {source}")
            : fileName;
    }

    public static ZipArchiveEntry FindFileEntry(ZipArchive archive, string source, string packagePath)
    {
        var matches = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.FullName, source, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new FileNotFoundException(
                $"Zip payload file not found: {source} in {packagePath}",
                source),
            _ => throw new InvalidOperationException($"Zip payload file matched multiple entries: {source}")
        };
    }

    public static void CopyEntryToNewFile(ZipArchiveEntry entry, string destinationPath)
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
            using (Stream input = entry.Open())
            using (FileStream output = File.Create(tempPath))
            {
                input.CopyTo(output);
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

    public static string ResolveUnderDirectory(string rootDirectory, string relativePath)
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
