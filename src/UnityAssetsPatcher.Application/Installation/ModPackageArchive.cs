using System.IO.Compression;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.Application.Installation;

public sealed class ModPackageArchive
{
    private readonly string _packagePath;
    private readonly IFileSystemOperations _fileSystemOperations;

    private const long MaxTotalModPackageExtractionSize = 10L * 1024L * 1024L * 1024L; // 10GB
    private const int CopyBufferSize = 81920;

    public ModPackageArchive(string packagePath, IFileSystemOperations fileSystemOperations)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        _packagePath = packagePath;
        _fileSystemOperations = fileSystemOperations;
    }

    public ZipArchive OpenRead()
    {
        return ZipFile.OpenRead(_packagePath);
    }

    public ZipArchiveEntry FindRequiredFileEntry(ZipArchive archive, string source)
    {
        string normalizedSource = source.Replace('\\', '/');
        var matches = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) &&
                            string.Equals(entry.FullName, normalizedSource, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new FileNotFoundException(
                $"Zip payload file not found: {normalizedSource} in {_packagePath}",
                normalizedSource),
            _ => throw new InvalidOperationException($"Zip payload file matched multiple entries: {normalizedSource}")
        };
    }

    public void CopyEntryToNewFile(
        ZipArchiveEntry entry,
        string destinationPath,
        ref long reservedUncompressedBytes)
    {
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);

        long declaredBytes = entry.Length;
        long reservedOverageBytes = 0;
        ReserveUncompressedBytes(entry, declaredBytes, ref reservedUncompressedBytes);

        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            _fileSystemOperations.CreateDirectory(destinationDirectory);
        }

        string tempPath = Path.Combine(
            string.IsNullOrEmpty(destinationDirectory) ? Directory.GetCurrentDirectory() : destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (Stream input = entry.Open())
            using (FileStream output = File.Create(tempPath))
            {
                byte[] buffer = new byte[CopyBufferSize];
                long copiedBytes = 0;
                int bytesRead;

                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    copiedBytes += bytesRead;
                    ReserveOverageIfNeeded(
                        entry,
                        declaredBytes,
                        copiedBytes,
                        ref reservedOverageBytes,
                        ref reservedUncompressedBytes);
                    output.Write(buffer, 0, bytesRead);
                }
            }

            try
            {
                File.Move(tempPath, destinationPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                throw new IOException(
                    $"Payload file was created by another process during installation: {destinationPath}");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ReserveOverageIfNeeded(
        ZipArchiveEntry entry,
        long declaredBytes,
        long copiedBytes,
        ref long reservedOverageBytes,
        ref long reservedUncompressedBytes)
    {
        long overageBytes = copiedBytes - declaredBytes;

        if (overageBytes <= reservedOverageBytes)
        {
            return;
        }

        long additionalBytes = overageBytes - reservedOverageBytes;
        ReserveUncompressedBytes(entry, additionalBytes, ref reservedUncompressedBytes);
        reservedOverageBytes += additionalBytes;
    }

    private static void ReserveUncompressedBytes(ZipArchiveEntry entry, long bytes, ref long reservedUncompressedBytes)
    {
        if (reservedUncompressedBytes > MaxTotalModPackageExtractionSize - bytes)
        {
            long totalBytes = reservedUncompressedBytes + bytes;

            throw new InvalidOperationException(
                $"Zip package exceeds the maximum allowed total uncompressed size while extracting {entry.FullName}: " +
                $"{totalBytes} bytes > {MaxTotalModPackageExtractionSize} bytes.");
        }

        reservedUncompressedBytes += bytes;
    }
}
